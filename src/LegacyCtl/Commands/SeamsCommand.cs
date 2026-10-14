using System.Text.RegularExpressions;
using LegacyCtl.Estate;
using LegacyDemo.Shared;
using static LegacyDemo.Shared.Html;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy seams: why can't I test this, and what is the smallest change that fixes it?
///
/// A seam (Feathers) is a place where you can alter behavior without editing in that
/// place. In .NET the cheapest seam there is, almost every time, is a constructor
/// parameter. This command finds what a method reaches for, including what it reaches
/// for indirectly through fields its constructor filled in.
/// </summary>
public static class SeamsCommand
{
    public static int Run(Paths paths, CommandLine cli)
    {
        var target = cli.Arg(0) ?? throw new ArgumentException("usage: legacy seams <file.cs> [--method Name]");
        var full = paths.Abs(target);
        if (!File.Exists(full)) throw new FileNotFoundException($"No such file: {target}");
        var model = EstateModel.Load(paths.Estate);
        var file = model.FindFile(full) ?? EstateModel.ReadSource(paths.Estate, "?", full);
        var cls = file.ClassName ?? Path.GetFileNameWithoutExtension(full);
        var method = cli.Get("method");
        var ctorRows = new List<string[]>();
        var blockerHtml = new System.Text.StringBuilder();
        string? seamHtml = null;

        Con.Header("legacy seams  " + paths.Rel(full), method is null ? cls : cls + "." + method);

        // Constructors, and what they assign from a blocker.
        var ctors = FindMembers(file.Lines, @"^\s*public\s+" + Regex.Escape(cls) + @"\s*\(([^)]*)\)");
        var fieldBlockers = new Dictionary<string, Hit>();
        Con.Section("CONSTRUCTORS");
        foreach (var (sig, start, end) in ctors)
        {
            var hits = HitsIn(file, start, end).ToList();
            var note = hits.Count == 0 ? Con.C(Con.Green, "no ambient dependencies") : string.Join("; ", hits.Select(h => h.Rule.Label));
            Con.Line($"   {sig.Trim(),-44} {note}");
            ctorRows.Add(new[]
            {
                $"<span class=\"mono\">{E(sig.Trim())}</span>",
                hits.Count == 0 ? Badge("no ambient dependencies", Tone.Good)
                                : string.Join(" ", hits.Select(h => Badge(h.Rule.Label, Tone.Bad))),
            });
            for (var i = start; i <= end; i++)
            {
                var m = Regex.Match(file.Lines[i - 1], @"\b(_\w+)\s*=\s*(.+);");
                if (!m.Success) continue;
                var hit = hits.FirstOrDefault(h => h.Line == i);
                if (hit is not null) fieldBlockers[m.Groups[1].Value] = hit;
            }
        }
        if (ctors.Count == 0) Con.Line("   " + Con.C(Con.Grey, "none declared (implicit default constructor)"));
        var constructions = model.Files.Sum(f => f.Lines.Count(l => !Rules.IsComment(l) && l.Contains("new " + cls + "(", StringComparison.Ordinal)));
        if (constructions > 0)
            Con.Note($"{constructions} places in the estate construct {cls} with 'new'. Any seam you add must not change them.", 3);

        IEnumerable<Hit> scope;
        int from = 1, to = file.Lines.Length;
        if (method is not null)
        {
            var m = FindMembers(file.Lines, @"^\s*public\s+[\w<>\[\]\?]+\s+" + Regex.Escape(method) + @"\s*\(").FirstOrDefault();
            if (m.Signature is null) throw new ArgumentException($"No public method '{method}' in {cls}.");
            (from, to) = (m.Start, m.End);
        }
        scope = HitsIn(file, from, to);

        Con.Section(method is null ? "BLOCKERS IN THIS FILE" : $"WHAT {method.ToUpperInvariant()} REACHES FOR");
        var shown = 0;
        foreach (var g in scope.GroupBy(h => h.Rule.Id))
        {
            var list = g.ToList();
            Con.Line($"   {Con.Fail(list[0].Rule.Id),-26} x{list.Count,-3} {list[0].Rule.Label,-34} -> {Con.C(Con.Teal, list[0].Rule.Seam)}");
            foreach (var h in list.Take(method is null ? 2 : 4))
                Con.Line(Con.C(Con.Grey, $"      {h.Line,4}  {h.Snippet(72)}"));
            shown += list.Count;
            blockerHtml.Append(BlockerCard(list[0].Rule.Id, list.Count, list[0].Rule.Label, list[0].Rule.Seam,
                list.Take(method is null ? 2 : 4).Select(h => ((int?)h.Line, file.Lines[h.Line - 1].Trim(), "hot"))));
        }
        // Indirect: fields the constructor filled from a blocker.
        if (method is not null)
        {
            foreach (var (field, hit) in fieldBlockers)
            {
                var uses = Enumerable.Range(from, to - from + 1).Where(i => Regex.IsMatch(file.Lines[i - 1], @"\b" + Regex.Escape(field) + @"\b")).ToList();
                if (uses.Count == 0) continue;
                Con.Line($"   {Con.Fail("via-constructor"),-26} x{uses.Count,-3} {("uses " + field + ", set in the ctor"),-34} -> {Con.C(Con.Teal, hit.Rule.Seam)}");
                Con.Line(Con.C(Con.Grey, $"      {uses[0],4}  {MapCommand.Trunc(file.Lines[uses[0] - 1].Trim(), 70)}"));
                Con.Line(Con.C(Con.Grey, $"      {hit.Line,4}  {hit.Snippet(56)}   (constructor)"));
                shown += uses.Count;
                blockerHtml.Append(BlockerCard("via-constructor", uses.Count, $"uses {field}, which the constructor set", hit.Rule.Seam, new[]
                {
                    ((int?)uses[0], file.Lines[uses[0] - 1].Trim(), "hot"),
                    ((int?)null, "...", "dim"),
                    ((int?)hit.Line, file.Lines[hit.Line - 1].Trim() + "   // in the constructor", "hot"),
                }));
            }
        }
        if (shown == 0) Con.Line("   " + Con.Pass("NONE") + " nothing ambient. This code can already run in a test.");

        if (method is not null && shown > 0)
        {
            Con.Section("SMALLEST SEAM THAT UNBLOCKS IT");
            var needs = scope.Select(h => h.Rule.Id).Concat(fieldBlockers.Values.Select(h => h.Rule.Id)).Distinct().ToList();
            var parameters = new List<string>();
            if (needs.Contains("static-db")) parameters.Add("I" + cls.Replace("Manager", "") + "Data data");
            if (needs.Contains("ambient-time")) parameters.Add("TimeProvider clock");
            if (needs.Contains("ambient-config")) parameters.Add("SomeOptions options");
            Con.Line($"   Parameterize Constructor:  {Con.C(Con.Teal, $"public {cls}({string.Join(", ", parameters)})")}");
            Con.Line($"   and keep the old one:      {Con.C(Con.Teal, $"public {cls}() : this(<production defaults>)")}");
            seamHtml = Section("Parameterize Constructor", "The smallest seam that unblocks it",
                Code(new (int?, string, string)[]
                {
                    (null, "// new: everything the method reaches for, handed in", "dim"),
                    (null, $"public {cls}({string.Join(", ", parameters)})", "add"),
                    (null, "", ""),
                    (null, "// kept: every existing call site still compiles and still gets production", "dim"),
                    (null, $"public {cls}() : this(<production defaults>)", "add"),
                }, cls + ".cs") +
                Callout("Zero call sites change. No rewrite, no mocking library.", Tone.Good));
            Con.Line();
            Con.Note($"Zero call sites change. Production still gets the database and the wall clock. A test gets " +
                     $"recorded data and a clock pinned to whatever day it likes. No container, no mocking library, " +
                     $"no rewrite: one new constructor{(parameters.Count(x => !x.StartsWith("TimeProvider")) is var n && n > 0 ? $", {n} small interface{(n == 1 ? "" : "s")}" : "")}" +
                     (parameters.Any(x => x.StartsWith("TimeProvider")) ? ", and TimeProvider, which has been in the BCL since .NET 8." : "."), 3);
        }
        Con.Line();

        if (cli.Get("html") is { } htmlPath)
        {
            var head = Tiles(
                ("blockers " + (method is null ? "in this file" : "in " + method), N(shown), shown == 0 ? Tone.Good : Tone.Bad),
                ("constructors", N(ctors.Count), Tone.Info),
                ("places that call new " + cls + "()", N(constructions), Tone.Warn));
            var ctorHtml = H3("Constructors") + (ctorRows.Count == 0 ? P("None declared (implicit default constructor).", "note")
                : Table(new[] { "signature", "what it reaches for" }, ctorRows));
            if (constructions > 0)
                ctorHtml += Callout($"{constructions} callers use new {cls}(). None of them may change.", Tone.Warn);
            var reach = H3(method is null ? "Blockers in this file" : $"What {method} reaches for") +
                        (shown == 0 ? Verdict("NONE", "Nothing ambient. This code can already run in a test.", Tone.Good) : blockerHtml.ToString());
            Html.Emit(htmlPath, Section("legacy seams · " + paths.Rel(full), method is null ? cls : $"Why can't {cls}.{method} run in a test?",
                head + ctorHtml + reach) + (seamHtml ?? ""));
        }
        return 0;
    }

    private static string BlockerCard(string id, int count, string label, string seam, IEnumerable<(int?, string, string)> lines) =>
        $"<div class=\"mini\" style=\"margin:10px 0\">{Badge(id, Tone.Bad)} <b>×{count}</b> <span class=\"note\">{E(label)}</span>" +
        $" <span class=\"mono\" style=\"color:var(--muted)\">&#8594;</span> {Chip(seam)}{Code(lines)}</div>";

    private static IEnumerable<Hit> HitsIn(SourceFile file, int start, int end) =>
        file.Hits.Where(h => h.Line >= start && h.Line <= end);

    /// <summary>Finds members by signature regex and returns their line span by brace matching.</summary>
    private static List<(string Signature, int Start, int End)> FindMembers(string[] lines, string signature)
    {
        var re = new Regex(signature);
        var result = new List<(string, int, int)>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!re.IsMatch(lines[i])) continue;
            int depth = 0, j = i;
            var opened = false;
            for (; j < lines.Length; j++)
            {
                foreach (var ch in Rules.CodeOf(lines[j]))
                {
                    if (ch == '{') { depth++; opened = true; }
                    else if (ch == '}') depth--;
                }
                if (opened && depth <= 0) break;
            }
            result.Add((lines[i].Trim().TrimEnd('{').Trim(), i + 1, Math.Min(j, lines.Length - 1) + 1));
        }
        return result;
    }
}
