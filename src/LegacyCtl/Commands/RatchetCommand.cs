using System.Text.Json;
using LegacyCtl.Estate;
using LegacyDemo.Shared;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy ratchet: the legacy code does not get to grow.
///
/// You will not remove 400 DateTime.Now calls this quarter. You can make sure there is
/// never a 401st. The build counts every seam blocker, per rule and per file, against a
/// committed baseline. More than the baseline fails the build. Fewer tightens it.
/// Same mechanism as [Obsolete] + TreatWarningsAsErrors on an API you are retiring,
/// for patterns the compiler cannot see.
/// </summary>
public static class RatchetCommand
{
    public sealed class Baseline
    {
        public Dictionary<string, Dictionary<string, int>> Rules { get; set; } = new();
    }

    public static Dictionary<string, Dictionary<string, int>> Count(EstateModel model)
    {
        var result = Rules.Blockers.ToDictionary(r => r.Id, _ => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        foreach (var f in model.Files)
            foreach (var g in f.Hits.GroupBy(h => h.Rule.Id))
                result[g.Key][f.RelPath] = g.Count();
        return result;
    }

    public static int Run(Paths paths, CommandLine cli)
    {
        var estate = cli.Get("estate") is { } e ? paths.Abs(e) : paths.Estate;
        var baselinePath = cli.Get("baseline") is { } b ? paths.Abs(b) : paths.RatchetBaseline;
        var model = EstateModel.Load(estate);
        var now = Count(model);
        if (!File.Exists(baselinePath))
        {
            Write(baselinePath, now);
            Con.Line();
            Con.Line($"   {Con.Pass()} no baseline yet; wrote one from the current estate: {paths.Rel(baselinePath)}");
            Con.Line();
            return 0;
        }
        var baseline = JsonSerializer.Deserialize<Baseline>(File.ReadAllText(baselinePath), Json)!;

        Con.Header("legacy ratchet", "seam blockers may go down, never up   (" + paths.Rel(baselinePath) + ")");
        Con.Line(Con.C(Con.Grey, "   rule                 baseline    now"));
        var grew = new List<(string Rule, string File, int Was, int Now)>();
        var shrank = 0;
        foreach (var rule in Rules.Blockers)
        {
            var was = baseline.Rules.TryGetValue(rule.Id, out var bf) ? bf : new Dictionary<string, int>();
            var cur = now[rule.Id];
            int wasTotal = was.Values.Sum(), nowTotal = cur.Values.Sum();
            var mark = nowTotal > wasTotal ? Con.Fail() : nowTotal < wasTotal ? Con.Pass("DOWN") : Con.C(Con.Grey, "  ok  ");
            Con.Line($"   {rule.Id,-18} {wasTotal,9} {nowTotal,6}   {mark}");
            foreach (var (file, count) in cur)
            {
                var before = was.TryGetValue(file, out var c) ? c : 0;
                if (count > before) grew.Add((rule.Id, file, before, count));
            }
            if (nowTotal < wasTotal) shrank += wasTotal - nowTotal;
        }

        Con.Line();
        if (grew.Count > 0)
        {
            var added = grew.Sum(g => g.Now - g.Was);
            Con.Line($"   {Con.Fail()} this change adds {added} new seam blocker{(added == 1 ? "" : "s")}:");
            foreach (var g in grew)
            {
                var file = model.Files.First(f => f.RelPath == g.File);
                foreach (var h in file.Hits.Where(h => h.Rule.Id == g.Rule).Take(Math.Max(1, g.Now - g.Was)))
                {
                    Con.Line($"      {h.File}:{h.Line}");
                    Con.Line(Con.C(Con.Grey, $"         {h.Snippet(64)}"));
                    Con.Line($"         {Con.C(Con.Amber, h.Rule.Label)} -> {Con.C(Con.Teal, h.Rule.Seam)}");
                }
            }
            Con.Line();
            Con.Note("New code does not get to be legacy code. Take the dependency as a constructor parameter instead; " +
                     "the existing blockers are not your problem today, but these are.", 3);
            Con.Line();
            Html.Emit(cli.Get("html"), Views.Ratchet(now, baseline, grew, model));
            return 1;
        }

        if (cli.Has("update"))
        {
            Write(baselinePath, now);
            Con.Line($"   {Con.Pass()} baseline {(shrank > 0 ? $"tightened by {shrank}" : "rewritten")}: {paths.Rel(baselinePath)}");
        }
        else
        {
            Con.Line($"   {Con.Pass()} no new seam blockers." + (shrank > 0 ? $" {shrank} fewer than baseline; run with --update to lock that in." : ""));
        }
        Con.Line();
        Html.Emit(cli.Get("html"), Views.Ratchet(now, baseline, grew, model));
        return 0;
    }

    private static void Write(string path, Dictionary<string, Dictionary<string, int>> counts)
    {
        var nb = new Baseline
        {
            Rules = counts.Where(kv => kv.Value.Count > 0)
                          .ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(f => f.Key, StringComparer.Ordinal).ToDictionary(f => f.Key, f => f.Value)),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(nb, Json) + Environment.NewLine);
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
}
