using System.Diagnostics;
using System.Globalization;
using LegacyDemo.Shared;
using Tallgrass.Billing.Characterization;
using Tallgrass.Data;
using static LegacyDemo.Shared.Html;

// -----------------------------------------------------------------------------
// characterize - pin down what BillingManager.CalculateLateFee DOES, not what it
// should do. Every row in fixtures/golden/late-fees.csv is an input and the fee the
// legacy code produced for it, recorded from a production snapshot.
//
// A characterization test has no opinion. It is not asserting that the answer is
// right; it is asserting that the answer has not changed. That is exactly what you
// need before you touch code that month-end close depends on.
// -----------------------------------------------------------------------------

Con.Init(args.Contains("--no-color"));
var root = RepoRoot.Find(ArgValue("--repo"));
var casesPath = Path.Combine(root, "fixtures", "golden", "late-fees.csv");
var cases = GoldenCase.Load(casesPath);
var htmlPath = ArgValue("--html");
var eyebrow = "characterize · BillingManager.CalculateLateFee";
var approach = Chip("approach: " + LateFeeCharacterization.Approach);

Con.Header("characterize  BillingManager.CalculateLateFee",
           $"{Con.N(cases.Count)} recorded cases  ({Path.GetRelativePath(root, casesPath)})");
Con.Line("  approach: " + Con.C(Con.Teal, LateFeeCharacterization.Approach));

var mismatches = new List<(GoldenCase Case, decimal Actual)>();
var blocked = 0;
Exception? firstError = null;
var sw = Stopwatch.StartNew();

foreach (var c in cases)
{
    try
    {
        var actual = LateFeeCharacterization.Calculate(c);
        if (actual != c.RecordedFee) mismatches.Add((c, actual));
    }
    catch (Exception ex)
    {
        blocked++;
        firstError ??= ex;
    }
}
sw.Stop();

Con.Line();
if (blocked > 0)
{
    Con.Line($"  {Con.Fail("BLOCKED")} {Con.N(blocked)} of {Con.N(cases.Count)} cases could not run.");
    Con.Line();
    var root0 = firstError!;
    while (root0.InnerException is not null) root0 = root0.InnerException;
    foreach (var line in Con.Wrap(root0.GetType().Name + ": " + root0.Message, Con.Width - 6))
        Con.Line("      " + Con.C(Con.Red, line));
    foreach (var frame in TallgrassFrames(firstError!))
        Con.Line("        " + Con.C(Con.Grey, "at " + frame));
    Con.Line();
    Con.Say("This is not a failing test. It is a test that cannot exist yet: the only way to " +
            "construct BillingManager is to connect to SQL Server.");
    var days = cases.Select(x => x.AsOf.Date).Distinct().Count();
    Con.Say($"And with a database it still could not pass. CalculateLateFee reads DateTime.Now, " +
            $"and these cases were recorded on {Con.N(days)} different days.");
    Con.Line();
    var frames = TallgrassFrames(firstError!).ToList();
    Emit(htmlPath, Section(eyebrow, "The test you write on day one cannot run",
        Tiles(("cases blocked", $"{N(blocked)} of {N(cases.Count)}", Tone.Bad), ("cases that ran", "0", Tone.Muted),
              ("different days the cases were recorded on", N(days), Tone.Warn)) + approach +
        Verdict("BLOCKED", "Not a failing test. A test that cannot exist yet.", Tone.Bad) +
        H3("Why") +
        Code(new (int?, string, string)[] { (null, root0.GetType().Name + ": " + root0.Message, "del") }
             .Concat(frames.Select(f => ((int?)null, "   at " + f, "dim"))), "first exception, Tallgrass frames only") +
        (frames.Count > 1 ? Flow(frames.Select(f => f.Replace("Tallgrass.", "")).ToArray()) : "") +
        Grid(new[]
        {
            MiniCard(Badge("static-db", Tone.Bad), "It opens its own database",
                "The only way to construct BillingManager is to connect to SQL Server. There is no constructor a test can call."),
            MiniCard(Badge("ambient-time", Tone.Bad), "It reads the wall clock",
                $"CalculateLateFee reads DateTime.Now, and these cases were recorded on {N(days)} different days. Even with a database, it could not pass."),
        }, 2),
        $"{N(cases.Count)} recorded cases from {Path.GetRelativePath(root, casesPath).Replace('\\', '/')}"));
    return 2;
}

if (mismatches.Count == 0)
{
    Con.Line($"  {Con.Pass()} {Con.N(cases.Count)} of {Con.N(cases.Count)} recorded cases match, to the cent.  " +
             Con.C(Con.Grey, $"({sw.ElapsedMilliseconds} ms)"));
    Con.Line();
    Con.Note("Pinned: grace periods, weekend roll-forward, autopay first-offence waiver, hardship, " +
             "min/max fee, the repeat-offender multiplier, and whatever the rounding does.");
    Con.Line();
    var classes = cases.GroupBy(c => c.RateClass).Select(g => Chip($"{g.Key} × {g.Count()}"));
    Emit(htmlPath, Section(eyebrow, "Behavior pinned, to the cent",
        Tiles(("recorded cases match", $"{N(cases.Count)} of {N(cases.Count)}", Tone.Good), ("changed", "0", Tone.Good),
              ("to run all of them", $"{sw.ElapsedMilliseconds} ms", Tone.Info)) + approach +
        Verdict("PASS", $"{N(cases.Count)} of {N(cases.Count)} recorded cases match, to the cent.", Tone.Good) +
        $"<p class=\"note\">Rate classes: {string.Concat(classes)}</p>" +
        Callout("Behavior pinned. Now it is safe to change.", Tone.Good),
        "No database. A clock pinned to the day each case was recorded."));
    return 0;
}

Con.Line($"  {Con.Fail()} {Con.N(mismatches.Count)} of {Con.N(cases.Count)} recorded cases changed behavior.");
Con.Line();
Con.Line(Con.C(Con.Grey, "    case     class          past due    recorded        now      diff"));
foreach (var (c, actual) in mismatches.Take(12))
{
    var diff = actual - c.RecordedFee;
    Con.Line($"    {c.CaseId,-8} {c.RateClass,-12} {Con.M(c.PastDue),10} {Con.M(c.RecordedFee),11} {Con.M(actual),10}   " +
             Con.C(Con.Red, (diff > 0 ? "+" : "") + Con.M(diff)));
}
if (mismatches.Count > 12) Con.Line(Con.C(Con.Grey, $"    ... and {mismatches.Count - 12} more"));
Con.Line();
if (mismatches.All(m => Math.Abs(m.Actual - m.Case.RecordedFee) == 0.01m))
{
    Con.Say("Every difference is exactly one cent. That smells like midpoint rounding.");
    Con.Note("Math.Round(x, 2) rounds half to even. That is not a bug in the old code; it is ten " +
             "years of invoices. Changing it is a business decision, not a refactor.");
}
Con.Line();
var oneCent = mismatches.All(m => Math.Abs(m.Actual - m.Case.RecordedFee) == 0.01m);
Emit(htmlPath, Section(eyebrow, "The refactor changed behavior",
    Tiles(("recorded cases changed", $"{N(mismatches.Count)} of {N(cases.Count)}", Tone.Bad),
          ("still match", N(cases.Count - mismatches.Count), Tone.Good),
          ("largest difference", "$" + M(mismatches.Max(m => Math.Abs(m.Actual - m.Case.RecordedFee))), Tone.Warn)) + approach +
    Stack(("unchanged", cases.Count - mismatches.Count, Tone.Good), ("changed", mismatches.Count, Tone.Bad)) +
    Verdict("FAIL", $"{N(mismatches.Count)} of {N(cases.Count)} recorded cases changed behavior.", Tone.Bad) +
    Table(new[] { "case", "rate class", "past due", "recorded", "now", "diff" }, mismatches.Take(12).Select(m => new[]
    {
        $"<span class=\"mono\">{E(m.Case.CaseId)}</span>", E(m.Case.RateClass.ToString()),
        $"<span class=\"mono\">{M(m.Case.PastDue)}</span>", $"<span class=\"mono\">{M(m.Case.RecordedFee)}</span>",
        $"<span class=\"mono\">{M(m.Actual)}</span>",
        Badge((m.Actual > m.Case.RecordedFee ? "+" : "") + M(m.Actual - m.Case.RecordedFee), Tone.Bad),
    })) +
    (mismatches.Count > 12 ? P($"... and {mismatches.Count - 12} more", "note") : "") +
    (oneCent ? Callout("Every difference is one cent: midpoint rounding. That is ten years of invoices, not a bug.", Tone.Warn) : "")));
return 1;

static IEnumerable<string> TallgrassFrames(Exception ex)
{
    var frames = new List<string>();
    for (var e = ex; e is not null; e = e.InnerException)
    {
        foreach (var f in new StackTrace(e, false).GetFrames())
        {
            var m = f.GetMethod();
            var type = m?.DeclaringType?.FullName;
            if (type is null || !type.StartsWith("Tallgrass.", StringComparison.Ordinal)) continue;
            if (type.StartsWith("Tallgrass.Billing.Characterization", StringComparison.Ordinal)) continue;
            frames.Add(type + "." + m!.Name);
        }
    }
    frames.Reverse();   // innermost last reads like a call chain on a slide
    return frames.Distinct().Reverse();
}

string? ArgValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

namespace Tallgrass.Billing.Characterization
{
    public sealed class GoldenCase
    {
        public required string CaseId { get; init; }
        public required int AccountId { get; init; }
        public required DateTime AsOf { get; init; }
        public required DateTime DueDate { get; init; }
        public required decimal PastDue { get; init; }
        public required RateClass RateClass { get; init; }
        public required bool AutoPay { get; init; }
        public required bool Hardship { get; init; }
        public required int LateCount12Months { get; init; }
        public required decimal RecordedFee { get; init; }

        public Account ToAccount() => new()
        {
            AccountId = AccountId,
            AccountNumber = "TG-" + AccountId.ToString("D7", CultureInfo.InvariantCulture),
            RateClass = RateClass,
            DueDate = DueDate,
            PastDueAmount = PastDue,
            CurrentBalance = PastDue,
            AutoPay = AutoPay,
            HardshipProgram = Hardship,
            LateCount12Months = LateCount12Months,
            Status = "A",
        };

        public static List<GoldenCase> Load(string path)
        {
            var list = new List<GoldenCase>();
            foreach (var line in File.ReadLines(path).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                var p = line.Split(',');
                var inv = CultureInfo.InvariantCulture;
                list.Add(new GoldenCase
                {
                    CaseId = p[0],
                    AccountId = int.Parse(p[1], inv),
                    AsOf = DateTime.ParseExact(p[2], "yyyy-MM-dd", inv),
                    DueDate = DateTime.ParseExact(p[3], "yyyy-MM-dd", inv),
                    PastDue = decimal.Parse(p[4], inv),
                    RateClass = Enum.Parse<RateClass>(p[5]),
                    AutoPay = p[6] == "1",
                    Hardship = p[7] == "1",
                    LateCount12Months = int.Parse(p[8], inv),
                    RecordedFee = decimal.Parse(p[9], inv),
                });
            }
            return list;
        }
    }

    /// <summary>A clock that says whatever the recorded case says it was.</summary>
    public sealed class FixedClock(DateTime localNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(localNow, DateTimeKind.Utc));
    }
}
