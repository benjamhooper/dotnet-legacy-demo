using System.Globalization;
using System.Text.Json;
using LegacyDemo.Shared;
using Tallgrass.FinalBill;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy shadow: replay recorded production traffic through the new code and explain
/// every difference before a single customer is routed to it.
///
/// The interesting part is not that differences exist. It is attributing each one to a
/// specific, nameable behavior of the old system, so the conversation with the business
/// is "do you want the 30-day month?" and not "the numbers are different, we don't know why".
/// </summary>
public static class ShadowCommand
{
    public sealed record Case(string Id, FinalBillRequest Request, decimal LegacyTotal);

    public sealed class KnownDifference
    {
        public string Category { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Ticket { get; set; } = "";
        public string Expires { get; set; } = "";
    }

    public sealed record Outcome(int Total, int Matched, Dictionary<string, List<(Case Case, decimal? Modern, string Detail)>> Explained,
                                 List<(Case Case, string Detail, KnownDifference? Known)> Rejected, int Unexplained, decimal NetDrift)
    {
        public int Accepted => Rejected.Count(r => r.Known is not null);
        public int Blocking => Unexplained + Explained.Values.Sum(v => v.Count) + Rejected.Count(r => r.Known is null);
        public bool Go => Blocking == 0;
    }

    public static int Run(Paths paths, CommandLine cli)
    {
        var casesPath = paths.Abs(cli.Get("cases") ?? "fixtures/shadow/final-bills.csv");
        var policyPath = paths.Abs(cli.Get("policy") ?? "modern/final-bill.policy.json");
        var knownPath = paths.Abs(cli.Get("known") ?? "modern/known-differences.json");
        var cases = Load(casesPath);
        var policy = ProrationPolicy.Load(policyPath);
        var known = LoadKnown(knownPath);
        var outcome = Evaluate(cases, policy, known);

        var days = cases.Count == 0 ? 0 : 30;
        Con.Header("legacy shadow  final-bill", $"{Con.N(cases.Count)} recorded requests, {days} days of production shadow traffic");
        Con.Line($"   legacy   {Con.C(Con.Teal, "dbo.usp_ProrateFinalBill")}   {Con.C(Con.Grey, "(what production returned)")}");
        Con.Line($"   modern   {Con.C(Con.Teal, "FinalBillCalculator")}        {Con.C(Con.Grey, "policy: " + policy.Describe())}");
        Con.Line();

        var differ = outcome.Total - outcome.Matched;
        if (differ == 0)
        {
            Con.Line($"   {Con.Pass("MATCH")} {Con.N(outcome.Total)} of {Con.N(outcome.Total)} identical, to the cent.");
        }
        else
        {
            var marker = outcome.Go ? Con.Pass("MATCH") : Con.Fail("DIFFER");
            Con.Line($"   {marker} {Con.N(outcome.Matched)} identical   {Con.N(differ)} different   " +
                     (outcome.NetDrift == 0m ? "" : Con.C(Con.Grey, $"modern would have billed ${Con.M(Math.Abs(outcome.NetDrift))} {(outcome.NetDrift < 0 ? "less" : "more")} in 30 days")));
        }

        if (outcome.Explained.Count > 0)
        {
            Con.Section("   EXPLAINED BY A LEGACY BEHAVIOR                         cases   example");
            foreach (var (label, desc) in Behaviors)
            {
                var touching = outcome.Explained.Where(kv => kv.Key.Split(" + ").Contains(label)).SelectMany(kv => kv.Value).ToList();
                if (touching.Count == 0) continue;
                var alone = outcome.Explained.TryGetValue(label, out var solo) && solo.Count > 0 ? solo[0] : touching[0];
                Con.Line($"   {Con.Warn("QUIRK")} {label,-19} {desc,-31} {touching.Count,5}   {alone.Case.Id}");
                Con.Line(Con.C(Con.Grey, $"           e.g. {alone.Detail}"));
            }
            var multi = outcome.Explained.Where(kv => kv.Key.Contains(" + ")).Sum(kv => kv.Value.Count);
            if (multi > 0)
                Con.Line(Con.C(Con.Grey, $"           ({Con.N(multi)} requests only match once two of these are switched on together)"));
        }

        if (outcome.Rejected.Count > 0)
        {
            Con.Section("   MODERN REFUSED WHAT LEGACY ACCEPTED");
            foreach (var g in outcome.Rejected.GroupBy(r => r.Known is not null))
            {
                var first = g.First();
                var mark = g.Key ? Con.Info("ACCEPTED") : Con.Fail("UNEXPLAINED");
                Con.Line($"   {mark} {g.Count(),3}   {first.Case.Id}  {first.Detail}");
                if (g.Key && first.Known is { } k)
                {
                    Con.Line(Con.C(Con.Grey, $"                owner {k.Owner}   ticket {k.Ticket}   expires {k.Expires}"));
                    Con.Note("\"" + k.Reason + "\"", 16);
                }
            }
        }
        if (outcome.Unexplained > 0)
            Con.Line($"   {Con.Fail("UNEXPLAINED")} {outcome.Unexplained} differences no single legacy behavior accounts for.");

        Con.Line();
        if (outcome.Go)
        {
            Con.Line($"   {Con.Pass("GO")} every difference is explained or explicitly accepted. Safe to route a slice of traffic.");
        }
        else
        {
            Con.Line($"   {Con.Fail("NO-GO")} {Con.N(outcome.Blocking)} requests would bill differently.");
            Con.Note("Nothing routes to modern until every difference is a decision: adopt the legacy behavior in the policy, or accept the difference with an owner and an expiry.", 11);
            if (outcome.Explained.Count > 0)
                Con.Note("The modern code is not wrong. It does what the tariff says. Production has been doing something else for years, and that something else is the spec now.", 11);
        }
        Con.Line();

        Directory.CreateDirectory(paths.Runtime);
        File.WriteAllText(Path.Combine(paths.Runtime, "shadow-final-bill.json"), JsonSerializer.Serialize(new
        {
            verdict = outcome.Go ? "GO" : "NO-GO",
            blocking = outcome.Blocking,
            policy = policy.Describe(),
            cases = outcome.Total,
        }));
        Html.Emit(cli.Get("html"), Views.Shadow(outcome, policy, Behaviors));
        return outcome.Go ? 0 : 1;
    }

    public static Outcome Evaluate(List<Case> cases, ProrationPolicy policy, List<KnownDifference> known)
    {
        var explained = new Dictionary<string, List<(Case, decimal?, string)>>();
        var rejected = new List<(Case, string, KnownDifference?)>();
        int matched = 0, unexplained = 0;
        decimal net = 0m;
        var modern = new FinalBillCalculator(policy);
        var toggles = Toggles(policy).ToList();

        foreach (var c in cases)
        {
            FinalBillResult result;
            try { result = modern.Calculate(c.Request); }
            catch (ArgumentException ex)
            {
                var detail = $"{ex.Message}; legacy billed {Money(c.LegacyTotal)}";
                rejected.Add((c, detail, known.FirstOrDefault(k => k.Category == "rejected-input")));
                continue;
            }
            if (result.Total == c.LegacyTotal) { matched++; continue; }
            net += result.Total - c.LegacyTotal;

            string? label = null;
            foreach (var subset in Subsets(toggles).OrderBy(s => s.Count))
            {
                var p = subset.Aggregate(policy, (acc, t) => t.Apply(acc));
                if (new FinalBillCalculator(p).Calculate(c.Request).Total == c.LegacyTotal)
                {
                    label = string.Join(" + ", subset.Select(t => t.Label));
                    break;
                }
            }
            if (label is null) { unexplained++; continue; }
            var r = c.Request;
            var detail2 = $"{r.CycleStart:MMM}, {Days(r)}: legacy {Money(c.LegacyTotal)} / modern {Money(result.Total)}";
            if (!explained.TryGetValue(label, out var list)) explained[label] = list = new();
            list.Add((c, result.Total, detail2));
        }
        return new Outcome(cases.Count, matched, explained, rejected, unexplained, net);
    }

    public static readonly (string Label, string Description)[] Behaviors =
    [
        ("monthBasis=Thirty", "divides by 30, every month"),
        ("rounding=PerDay", "rounds the daily rate first"),
        ("writeOffUnder=1.00", "writes off balances under $1"),
    ];

    private sealed record Toggle(string Label, Func<ProrationPolicy, ProrationPolicy> Apply);

    private static IEnumerable<Toggle> Toggles(ProrationPolicy p)
    {
        if (p.MonthBasis != MonthBasis.Thirty) yield return new("monthBasis=Thirty", x => x with { MonthBasis = MonthBasis.Thirty });
        if (p.Rounding != Rounding.PerDay) yield return new("rounding=PerDay", x => x with { Rounding = Rounding.PerDay });
        if (p.SmallBalanceWriteOffUnder is null) yield return new("writeOffUnder=1.00", x => x with { SmallBalanceWriteOffUnder = 1.00m });
    }

    private static IEnumerable<List<Toggle>> Subsets(List<Toggle> items)
    {
        for (var mask = 1; mask < 1 << items.Count; mask++)
            yield return items.Where((_, i) => (mask & (1 << i)) != 0).ToList();
    }

    private static string Days(FinalBillRequest r)
    {
        var n = r.MoveOut.DayNumber - r.CycleStart.DayNumber + 1;
        return n == 1 ? "1 day" : $"{n} days";
    }

    private static string Money(decimal d) => (d < 0 ? "-" : "") + "$" + Math.Abs(d).ToString("0.00", CultureInfo.InvariantCulture);

    public static List<Case> Load(string path)
    {
        var inv = CultureInfo.InvariantCulture;
        var list = new List<Case>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.StartsWith("request,", StringComparison.Ordinal) || line.Trim().Length == 0) continue;
            var p = line.Split(',');
            list.Add(new Case(p[0], new FinalBillRequest(
                int.Parse(p[1], inv),
                DateOnly.ParseExact(p[3], "yyyy-MM-dd", inv),
                DateOnly.ParseExact(p[4], "yyyy-MM-dd", inv),
                decimal.Parse(p[5], inv), decimal.Parse(p[6], inv), decimal.Parse(p[7], inv),
                decimal.Parse(p[8], inv), decimal.Parse(p[9], inv)),
                decimal.Parse(p[12], inv)));
        }
        return list;
    }

    public static List<KnownDifference> LoadKnown(string path)
    {
        if (!File.Exists(path)) return new();
        var doc = JsonSerializer.Deserialize<Dictionary<string, List<KnownDifference>>>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return doc is not null && doc.TryGetValue("final-bill", out var list) ? list : new();
    }
}
