using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LegacyDemo.Shared;
using static LegacyDemo.Shared.Html;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy page: stitch the HTML fragments one demo produced into a single page, then
/// rebuild reports/index.html so every demo that has run is one click away.
///
///   legacy page --dir reports/.parts/demo1 --out reports/demo1.html
///               --label "LIVE DEMO 1" --title "Map the project" --subtitle "..." [--result "GO:good"]
///
/// Fragments are concatenated in file-name order (01-..., 02-...). Each page carries a
/// one-line JSON comment so the index can describe it without parsing the page.
/// </summary>
public static class PageCommand
{
    /// <summary>The demo pages, in the order they happen in the talk.</summary>
    public static readonly (string File, string Short)[] Known =
    [
        ("demo1.html", "1 · map"),
        ("demo1-real.html", "1 · real code"),
        ("demo2.html", "2 · seam + pin"),
        ("demo2-break.html", "2 · break it"),
        ("demo3.html", "3 · shadow"),
        ("demo3-compat.html", "3 · compat + route"),
        ("demo4.html", "4 · triage + ratchet"),
        ("demo4-fix.html", "4 · fix"),
    ];

    private sealed record Meta(string Label, string Title, string Subtitle, string? Result, string Generated);

    public static int Run(Paths paths, CommandLine cli)
    {
        var dir = paths.Abs(cli.Get("dir") ?? throw new ArgumentException("usage: legacy page --dir <fragments> --out <page.html> --title <t>"));
        var outPath = paths.Abs(cli.Get("out") ?? throw new ArgumentException("legacy page needs --out <page.html>"));
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException($"No fragments in {paths.Rel(dir)}.");
        var label = cli.Get("label") ?? "LEGACY";
        var title = cli.Get("title") ?? Path.GetFileNameWithoutExtension(outPath);
        var subtitle = cli.Get("subtitle") ?? "";
        var result = cli.Get("result");

        var body = new StringBuilder();
        foreach (var f in Directory.EnumerateFiles(dir, "*.html").Order(StringComparer.Ordinal))
            body.Append(File.ReadAllText(f));
        if (result is not null)
        {
            var (word, tone) = ParseResult(result);
            body.Insert(0, $"<div class=\"result\">{Badge("result: " + word, tone)}</div>\n");
        }

        var outDir = Path.GetDirectoryName(outPath)!;
        Directory.CreateDirectory(outDir);
        var meta = new Meta(label, title, subtitle, result, DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        var page = Page(label, title, subtitle, body.ToString(), Nav(outDir, Path.GetFileName(outPath)), Path.GetFileName(outPath));
        File.WriteAllText(outPath, Redactor.Apply(MetaComment(meta) + page), new UTF8Encoding(false));
        WriteIndex(outDir);
        RefreshNav(outDir);

        Con.Line();
        Con.Line($"   {Con.Info("HTML")} {paths.Rel(outPath)}   " + Con.C(Con.Grey, "(Ctrl+click to open)"));
        Con.Line("          " + Con.Link(Con.FileUrl(outPath)));
        Con.Line("   " + Con.C(Con.Grey, "index  ") + Con.Link(Con.FileUrl(Path.Combine(outDir, "index.html"))));
        Con.Line();
        return 0;
    }

    private static (string Word, Tone Tone) ParseResult(string result)
    {
        var i = result.LastIndexOf(':');
        if (i < 0) return (result, Tone.Info);
        var tone = result[(i + 1)..].ToLowerInvariant() switch
        {
            "good" => Tone.Good, "warn" => Tone.Warn, "bad" => Tone.Bad, "muted" => Tone.Muted, _ => Tone.Info,
        };
        return (result[..i], tone);
    }

    private static string MetaComment(Meta m) =>
        "<!-- legacy-page " + JsonSerializer.Serialize(m).Replace("--", "\\u002d\\u002d") + " -->\n";

    private static Meta? ReadMeta(string path)
    {
        using var reader = new StreamReader(path);
        var first = reader.ReadLine() ?? "";
        var m = Regex.Match(first, @"^<!-- legacy-page (\{.*\}) -->$");
        return m.Success ? JsonSerializer.Deserialize<Meta>(m.Groups[1].Value) : null;
    }

    /// <summary>Index first, then every demo page that exists, in talk order.</summary>
    private static IEnumerable<(string File, string Label)> Nav(string outDir, string current)
    {
        yield return ("index.html", "all demos");
        foreach (var (file, label) in Known)
            if (file == current || File.Exists(Path.Combine(outDir, file)))
                yield return (file, label);
    }

    /// <summary>Pages written earlier didn't know about pages written later; give them all the current nav.</summary>
    private static void RefreshNav(string outDir)
    {
        foreach (var (file, _) in Known)
        {
            var path = Path.Combine(outDir, file);
            if (!File.Exists(path)) continue;
            var html = File.ReadAllText(path);
            var updated = Regex.Replace(html, "<nav>.*?</nav>", "<nav>" + NavLinks(Nav(outDir, file), file) + "</nav>", RegexOptions.Singleline);
            if (updated != html) File.WriteAllText(path, updated, new UTF8Encoding(false));
        }
    }

    public static void WriteIndex(string outDir)
    {
        var cards = new List<string>();
        foreach (var (file, shortLabel) in Known)
        {
            var path = Path.Combine(outDir, file);
            var meta = File.Exists(path) ? ReadMeta(path) : null;
            if (meta is null)
            {
                cards.Add($"<div class=\"mini off\">{Badge("not run yet", Tone.Muted)}<h4>{E(shortLabel)}</h4>" +
                          $"<p>Run <code>.\\demo.ps1 {E(Path.GetFileNameWithoutExtension(file))}</code> to generate it.</p></div>");
                continue;
            }
            var badge = meta.Result is null ? Badge("ran", Tone.Info) : Badge(ParseResult(meta.Result).Word, ParseResult(meta.Result).Tone);
            cards.Add($"<a class=\"mini link\" href=\"{E(file)}\">{badge}<div class=\"eyebrow\" style=\"margin-top:8px\">{E(meta.Label)}</div>" +
                      $"<h4>{E(meta.Title)}</h4><p>{E(meta.Subtitle)}</p><div class=\"meta\">{E(file)} · {E(meta.Generated)}</div></a>");
        }
        var body = Section("index", "Every demo, as a page", Grid(cards, 2),
            "Each page is rebuilt every time its demo runs. Open them from disk; nothing here needs a network.");
        var html = Page("REAL-WORLD .NET IN THE ENTERPRISE", "Demo reports",
            "What changes when your codebase has ten years of history", body, Nav(outDir, "index.html"), "index.html");
        File.WriteAllText(Path.Combine(outDir, "index.html"), html, new UTF8Encoding(false));
    }
}

/// <summary>
/// legacy show FILE --from 23 --to 30 --highlight 24,29: print a few numbered lines
/// of a file (console and, with --html, a code block). Replaces "open it in an editor".
/// </summary>
public static class ShowCommand
{
    public static int Run(Paths paths, CommandLine cli)
    {
        var target = cli.Arg(0) ?? throw new ArgumentException("usage: legacy show <file> [--from N] [--to N] [--highlight 3,5]");
        var full = paths.Abs(target);
        if (!File.Exists(full)) throw new FileNotFoundException($"No such file: {target}");
        var all = File.ReadAllLines(full);
        var from = Math.Max(1, cli.GetInt("from", 1));
        var to = Math.Min(all.Length, cli.GetInt("to", all.Length));
        var hot = (cli.Get("highlight") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToHashSet();

        Con.Line();
        Con.Line(Con.C(Con.Grey, "   " + paths.Rel(full)));
        for (var i = from; i <= to; i++)
        {
            var text = all[i - 1];
            Con.Line($"   {Con.C(Con.Grey, i.ToString(CultureInfo.InvariantCulture).PadLeft(4))}  {(hot.Contains(i) ? Con.C(Con.Amber, text) : text)}");
        }
        Con.Line();
        Html.Emit(cli.Get("html"), Views.Show(paths.Rel(full), all, from, to, hot, cli.Get("title")));
        return 0;
    }
}
