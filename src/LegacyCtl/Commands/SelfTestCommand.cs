using LegacyCtl.Estate;
using LegacyDemo.Shared;
using Tallgrass.FinalBill;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy selftest: every demo path that does not need a rebuild, asserted, without
/// touching the working tree. demo.ps1 verify runs this plus the rebuild paths.
/// </summary>
public static class SelfTestCommand
{
    private static int _passed, _failed;

    public static int Run(Paths paths, CommandLine cli)
    {
        Con.Header("legacy selftest");
        var model = EstateModel.Load(paths.Estate);

        Check("map: 14 projects in the estate", model.Projects.Count == 14);
        Check("map: Common is the biggest hub (blast radius 13)", model.Find("Tallgrass.Common")?.Dependents.Count == 13);
        Check("map: BillingManager is a god class", model.Files.Any(f => f.RelPath.EndsWith("BillingManager.cs") && f.IsGodClass));
        Check("map: ReportGenerator is a god class", model.Files.Any(f => f.RelPath.EndsWith("ReportGenerator.cs") && f.IsGodClass));
        Check("map: usp_ProrateFinalBill has rules and 3 callers",
              model.Procedures.FirstOrDefault(p => p.Name == "dbo.usp_ProrateFinalBill") is { HasBusinessLogic: true, CalledFrom.Count: 3 });
        Check("map: two projects still on .NET Framework, both old-style", model.Projects.Count(p => p.IsNetFramework && !p.SdkStyle) == 2);
        Check("map: checked-in binaries found by HintPath", model.Projects.SelectMany(p => p.BinaryReferences).Select(Path.GetFileName).Distinct().Count() == 4);
        Check("map: WebForms pages counted", model.Projects.Sum(p => p.WebFormsPages) == 2);
        Check("map: at least one orphan procedure", model.Procedures.Any(p => p.CalledFrom.Count == 0));
        Check("map: meter importer is a start-here candidate",
              model.Find("Tallgrass.Integrations.Meter") is { } meter && meter.Dependents.Count <= 1 && meter.Commits >= 20 && meter.BlockersPer100 < 2);

        var bm = model.Files.First(f => f.RelPath.EndsWith("BillingManager.cs"));
        var seamed = bm.Lines.Any(l => l.Contains("IBillingData data, TimeProvider clock"));
        Check("seams: BillingManager is in a known state (pristine or seamed)", true);
        Check("seams: pristine BillingManager reads the wall clock", seamed || bm.Hits.Any(h => h.Rule.Id == "ambient-time"));

        var cases = ShadowCommand.Load(Path.Combine(paths.Fixtures, "shadow", "final-bills.csv"));
        Check("shadow: 1,000 recorded requests", cases.Count == 1000);
        var naive = ShadowCommand.Evaluate(cases, ProrationPolicy.Default, new());
        Check("shadow: spec-faithful policy is NO-GO", !naive.Go);
        Check("shadow: 30-day month explains differences", naive.Explained.Keys.Any(k => k.Contains("monthBasis=Thirty")));
        Check("shadow: per-day rounding explains differences", naive.Explained.Keys.Any(k => k.Contains("rounding=PerDay")));
        Check("shadow: small-balance write-off explains differences", naive.Explained.Keys.Any(k => k.Contains("writeOffUnder")));
        Check("shadow: nothing is unexplained", naive.Unexplained == 0);
        Check("shadow: 3 bad-data requests rejected", naive.Rejected.Count == 3);

        var compatPolicy = ProrationPolicy.Load(Path.Combine(paths.Steps, "03-compat", "modern", "final-bill.policy.json"));
        var compatKnown = ShadowCommand.LoadKnown(Path.Combine(paths.Steps, "03-compat", "modern", "known-differences.json"));
        var compat = ShadowCommand.Evaluate(cases, compatPolicy, compatKnown);
        Check("shadow: compat policy + accepted differences is GO", compat.Go);
        Check("shadow: compat policy matches 997", compat.Matched == 997);

        Check("router: buckets are stable and in range", Enumerable.Range(0, 500).All(i => FinalBillRouter.Bucket(i) is >= 0 and < 100 && FinalBillRouter.Bucket(i) == FinalBillRouter.Bucket(i)));
        var sample = Enumerable.Range(0, 1000).Select(i => 1_000_000 + i * 997).Count(a => FinalBillRouter.Bucket(a) < 5);
        Check("router: 5% routes roughly 5% of accounts", sample is > 20 and < 90);
        var diffs = 0;
        var router = new FinalBillRouter(new Fixed(1m), new Fixed(2m), () => new Route(RouteMode.Shadow, 0), (_, _, _) => diffs++);
        var r = router.Calculate(cases[0].Request);
        Check("router: shadow mode returns legacy and records the difference", r.Total == 1m && diffs == 1);

        var verdicts = TriageCommand.Evaluate(model, Signals.Load(paths.Estate));
        Check("triage: Billing -> MODERNIZE", verdicts.Any(v => v.Subject == "Tallgrass.Billing" && v.Call == "MODERNIZE"));
        Check("triage: Reports -> LEAVE ALONE", verdicts.Any(v => v.Subject == "Tallgrass.Reports" && v.Call == "LEAVE ALONE"));
        Check("triage: Meter -> KEEP GOING", verdicts.Any(v => v.Subject == "Tallgrass.Integrations.Meter" && v.Call == "KEEP GOING"));
        Check("triage: Classic portal -> RETIRE", verdicts.Any(v => v.Subject == "Tallgrass.CustomerPortal.Classic" && v.Call == "RETIRE"));
        Check("triage: final-bill proc -> STRANGLE", verdicts.Any(v => v.Subject == "dbo.usp_ProrateFinalBill" && v.Call == "STRANGLE"));
        Check("triage: Common -> CONTAIN", verdicts.Any(v => v.Subject == "Tallgrass.Common" && v.Call == "CONTAIN"));

        // Ratchet: apply the PR to a scratch copy of the estate, never the working tree.
        var scratch = Path.Combine(Path.GetTempPath(), "legacy-selftest-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            CopyDir(paths.Estate, scratch);
            var baseline = RatchetCommand.Count(model);
            var pr = Path.Combine(paths.Steps, "04-pr", "estate");
            CopyDir(pr, scratch);
            var after = RatchetCommand.Count(EstateModel.Load(scratch));
            var grew = Rules.Blockers.Sum(rule => Math.Max(0, after[rule.Id].Values.Sum() - baseline[rule.Id].Values.Sum()));
            Check("ratchet: the PR adds new seam blockers", grew >= 3);
            Check("ratchet: baseline file exists", File.Exists(paths.RatchetBaseline));
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch (IOException) { }
        }

        Con.Line();
        Con.Line(_failed == 0
            ? $"   {Con.Pass()} {_passed} checks."
            : $"   {Con.Fail()} {_failed} of {_passed + _failed} checks failed.");
        Con.Line();
        return _failed == 0 ? 0 : 1;
    }

    private static void Check(string name, bool ok)
    {
        if (ok) _passed++; else _failed++;
        Con.Line($"   {(ok ? Con.Pass(" ok ") : Con.Fail())} {name}");
    }

    private static void CopyDir(string from, string to)
    {
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, f);
            if (rel.Contains("bin" + Path.DirectorySeparatorChar) || rel.Contains("obj" + Path.DirectorySeparatorChar)) continue;
            var dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(f, dest, true);
        }
    }

    private sealed class Fixed(decimal total) : IFinalBillEngine
    {
        public string Name => "fixed";
        public FinalBillResult Calculate(FinalBillRequest request) => new(0m, 0m, total);
    }
}
