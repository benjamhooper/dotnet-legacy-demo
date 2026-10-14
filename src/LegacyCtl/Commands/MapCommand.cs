using LegacyCtl.Estate;
using LegacyDemo.Shared;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy map: read a codebase before you touch it.
///
/// Five questions, answered from files you already have:
///   1. What depends on what, and what is the blast radius of each project?
///   2. Which classes are too big to hold in your head?
///   3. How much of the business logic is not in C# at all?
///   4. Where does change actually happen? (churn x size)
///   5. Given all that: where is it safe to start?
/// </summary>
public static class MapCommand
{
    public static int Run(Paths paths, CommandLine cli)
    {
        var estate = cli.Get("estate") is { } e ? paths.Abs(e) : paths.Estate;
        // The demo estate gets the talk's narration ("that is demo four"); anything else gets plain findings.
        var narrate = string.Equals(Path.GetFullPath(estate).TrimEnd('/', '\\'), Path.GetFullPath(paths.Estate).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);
        var model = EstateModel.Load(estate);
        var files = model.Files.ToList();
        var total = model.Projects.Count;
        if (total == 0) throw new InvalidOperationException($"No .csproj files under {paths.Rel(estate)}.");

        Con.Header("legacy map  " + paths.Rel(estate) + "/",
            $"{total} projects  {Con.N(files.Count)} C# files  {Con.N(files.Sum(f => f.CodeLines))} lines  {model.Procedures.Count} procedures");
        if (!narrate && model.History is not null) Con.Line(Con.C(Con.Grey, "   change history  " + model.History));
        if (!narrate && model.History is null)
            Con.Line(Con.C(Con.Amber, "   change history  none" + (EstateModel.GitError is null ? " (not a git repo)" : ": " + EstateModel.GitError)));

        // 1 ---------------------------------------------------------------
        Con.Section("1. DEPENDENCIES   who breaks if this changes?");
        Con.Line(Con.C(Con.Grey, "   project                              refs in  refs out  blast radius   lines  blockers"));
        foreach (var p in model.Projects.OrderByDescending(p => p.Dependents.Count).ThenBy(p => p.Name))
        {
            var radius = p.Dependents.Count;
            var tag = radius >= total / 2 ? Con.C(Con.Red, "HUB")
                    : radius == 0 ? Con.C(Con.Grey, "leaf")
                    : "";
            if (p.IsNetFramework) tag += (tag.Length > 0 ? " " : "") + Con.C(Con.Amber, p.Framework);
            var bar = Bar(radius, total - 1, 10);
            Con.Line($"   {p.Name,-36} {p.ReferencedBy.Count,6} {p.References.Count,9}   {bar} {radius,2}  {Con.N(p.Lines),6} {p.Blockers,8}  {tag}");
        }
        var hubs = model.Projects.Where(p => p.Dependents.Count >= total / 2).ToList();
        Con.Line();
        var frameworks = model.Projects.GroupBy(p => p.Framework).OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} x{g.Count()}");
        var oldStyle = model.Projects.Where(p => !p.SdkStyle).Select(p => Short(p.Name)).ToList();
        Con.Line($"   frameworks   {string.Join("   ", frameworks)}" +
                 (oldStyle.Count > 0 ? Con.C(Con.Grey, $"   (old-style .csproj: {string.Join(", ", oldStyle)})") : ""));
        var binaries = model.Projects.SelectMany(p => p.BinaryReferences.Select(b => (p.Name, Dll: Path.GetFileName(b))))
            .ToList();
        if (binaries.Count > 0)
            Con.Line($"   binaries     {Con.Warn("LIB")} {binaries.Select(b => b.Dll).Distinct().Count()} DLLs {(narrate ? "checked into lib/" : "checked in")} (HintPath, " +
                     $"{binaries.Select(b => b.Name).Distinct().Count()} projects)  " + Con.C(Con.Grey, "no package, no source"));
        var pages = model.Projects.Sum(p => p.WebFormsPages);
        if (pages > 0)
            Con.Line($"   web forms    {pages} .aspx pages   " + Con.C(Con.Grey, "System.Web has no in-place upgrade; only a strangler"));
        if (!narrate)
        {
            foreach (var (sln, listed, missing) in model.Solutions)
                Con.Line($"   solution     {sln}  lists {listed} projects" +
                         (missing == 0 ? "" : "  " + Con.Fail($"{missing} DO NOT EXIST")));
            Con.Line($"   tests        {Con.N(model.TestMethods)} test methods in {model.TestProjects} test project{(model.TestProjects == 1 ? "" : "s")}" +
                     Con.C(Con.Grey, $"   (one per {Con.N(model.TestMethods == 0 ? 0 : files.Sum(f => f.CodeLines) / model.TestMethods)} lines)"));
        }
        Con.Line();
        if (hubs.Count > 0)
            Con.Note($"{hubs.Count} hub projects. A change to {hubs.OrderByDescending(h => h.Dependents.Count).First().Name} " +
                     $"can break {hubs.Max(h => h.Dependents.Count)} of the other {total - 1}. That is not an architecture, it is a gravity well.", 3);

        // 2 ---------------------------------------------------------------
        Con.Section("2. GOD CLASSES   too big to hold in your head");
        Con.Line(Con.C(Con.Grey, "   file                                              lines  public  reaches  commits/12mo"));
        var gods = files.Where(f => f.IsGodClass).OrderByDescending(f => f.CodeLines).ToList();
        foreach (var f in gods.Take(narrate ? gods.Count : 8))
        {
            Con.Line($"   {Trunc(f.RelPath, 48),-48} {Con.N(f.CodeLines),6} {f.PublicMethods,7} {f.NamespacesReached,5} ns {f.Commits,9}  {Con.Warn("GOD")}");
        }

        if (gods.Count > 8 && !narrate) Con.Line(Con.C(Con.Grey, $"   ... and {gods.Count - 8} more ({gods.Count} god classes in all)"));

        // 3 ---------------------------------------------------------------
        Con.Section("3. LOGIC IN SQL   business rules the C# cannot see");
        Con.Line(Con.C(Con.Grey, "   procedure                          decisions  lines   called from"));
        foreach (var proc in model.Procedures.OrderByDescending(p => p.Decisions))
        {
            var callers = proc.CalledFrom.Count == 0
                ? Con.C(Con.Amber, "nothing in this repo (a job? a report? another app?)")
                : string.Join(", ", proc.CalledFrom.Select(Short));
            var mark = proc.HasBusinessLogic ? Con.Warn("RULES") : Con.C(Con.Grey, "  crud ");
            Con.Line($"   {proc.Name,-34} {proc.Decisions,5} {proc.CodeLines,8}   {mark} {callers}");
        }
        var rules = model.Procedures.Where(p => p.HasBusinessLogic).ToList();
        Con.Line();
        if (model.Procedures.Count == 0)
            Con.Note("No .sql files in this tree. That does not mean there is no logic in the database: it means it is not in source control.", 3);
        else if (!narrate && rules.Count == 0)
            Con.Note($"{model.Procedures.Count} .sql files, no business rules, none referenced from the C#. One-off fixes, run by hand. " +
                     "By whom, against which database? The rules themselves are somewhere this repo cannot see.", 3);
        else
            Con.Note($"{rules.Count} of {model.Procedures.Count} procedures make business decisions. " +
                     "None of them show up in a C# dependency graph, a code review of the C#, or a unit test.", 3);

        // 4 ---------------------------------------------------------------
        Con.Section("4. HOTSPOTS   change x size, last 12 months");
        var hot = files.Where(f => f.Commits > 0)
                       .Select(f => (f.RelPath, f.Commits, f.CodeLines, Score: f.Commits * Math.Log10(Math.Max(10, f.CodeLines))))
                       .Concat(model.Procedures.Where(p => p.Commits > 0)
                           .Select(p => (p.RelPath, p.Commits, p.CodeLines, Score: p.Commits * Math.Log10(Math.Max(10, p.CodeLines)))))
                       .OrderByDescending(x => x.Score).Take(6).ToList();
        if (hot.Count == 0) Con.Line("   " + Con.C(Con.Grey, "no change history (no .history/churn-12mo.tsv, no git)"));
        var maxScore = hot.Count == 0 ? 1 : hot.Max(h => h.Score);
        foreach (var h in hot)
            Con.Line($"   {Trunc(h.RelPath, 50),-50} {Bar(h.Score, maxScore, 16)} {h.Commits,3} commits  {Con.N(h.CodeLines),5} lines");

        var forks = files.SelectMany(f => f.Forks.Select(x => (x.Symbol, f.RelPath))).GroupBy(x => x.Symbol).ToList();
        if (forks.Count > 0)
        {
            Con.Line();
            foreach (var g in forks)
                Con.Line($"   {Con.Warn("FORK")} #if {g.Key,-14} {g.Count(),3} blocks in {g.Select(x => x.RelPath).Distinct().Count()} files  " +
                         Con.C(Con.Grey, "two programs share this source tree; only one of them is tested"));
        }

        // 5 ---------------------------------------------------------------
        Con.Section("5. WHERE TO START");
        foreach (var p in model.Projects.Where(p => !p.IsTest && p.Dependents.Count <= 1 && p.Commits >= 20 && p.BlockersPer100 < 2))
        {
            Con.Line($"   {Con.Pass("START HERE")}  {p.Name}");
            Con.Note($"changes every sprint ({p.Commits} commits), {(p.Dependents.Count == 0 ? "nothing" : "one project")} depends on it, " +
                     $"and it already has seams ({p.Blockers} blocker{(p.Blockers == 1 ? "" : "s")} in {Con.N(p.Lines)} lines). A win here is cheap and visible.", 17);
        }
        foreach (var f in files.Where(f => f.IsGodClass && f.Commits >= (narrate ? 20 : 10)).OrderByDescending(f => f.Commits).Take(3))
        {
            Con.Line($"   {Con.Warn("SEAM FIRST")}  {f.RelPath}");
            Con.Note((narrate ? "the hottest file in the estate and a god class" : $"a hotspot and a god class ({f.Commits} commits, {Con.N(f.CodeLines)} lines)") +
                     ". Do not rewrite it. Cut one seam at its edge, " +
                     $"pin its behavior, then move one method at a time.   ->  legacy seams {paths.Rel(Path.Combine(estate, f.RelPath))}", 17);
        }
        foreach (var proc in rules.Where(p => p.Commits >= 10 && p.CalledFrom.Count >= 2))
        {
            Con.Line($"   {Con.Warn("STRANGLE")}    {proc.Name}");
            Con.Note($"{proc.Decisions} business decisions, changed {proc.Commits} times this year, called from {proc.CalledFrom.Count} projects. " +
                     "Put one door in front of it before you replace what is behind the door.", 17);
        }
        foreach (var h in hubs.OrderByDescending(h => h.Dependents.Count).Take(narrate ? hubs.Count : 5))
        {
            Con.Line($"   {Con.Fail("NOT HERE")}    {h.Name,-24} {Con.C(Con.Grey, $"blast radius {h.Dependents.Count}. Contain it; do not start in it.")}");
        }
        Con.Line();

        if (cli.Get("mermaid") is { } mermaidPath)
        {
            var full = Path.GetFullPath(mermaidPath);
            File.WriteAllLines(full, Mermaid(model));
            Con.Note("dependency graph written to " + paths.Rel(full) + " (paste into any Mermaid renderer)");
            Con.Line();
        }
        if (!narrate && hubs.Count > 5) Con.Line(Con.C(Con.Grey, $"                 ... and {hubs.Count - 5} more hubs"));
        Html.Emit(cli.Get("html"), Views.Map(model, narrate));
        return 0;
    }

    public static IEnumerable<string> Mermaid(EstateModel model)
    {
        yield return "graph LR";
        foreach (var p in model.Projects)
            foreach (var r in p.References)
                yield return $"  {Id(p.Name)}[{Short(p.Name)}] --> {Id(r)}[{Short(r)}]";
        foreach (var proc in model.Procedures.Where(x => x.HasBusinessLogic))
            foreach (var c in proc.CalledFrom)
                yield return $"  {Id(c)} -.-> {Id(proc.Name)}[({proc.Name.Replace("dbo.", "")})]";
        static string Id(string s) => s.Replace(".", "_");
    }

    public static string Short(string project) => project.Replace("Tallgrass.", "");

    public static string Bar(double value, double max, int width)
    {
        var n = max <= 0 ? 0 : (int)Math.Round(value / max * width);
        return Con.C(Con.Teal, new string('#', n)) + Con.C(Con.Grey, new string('.', width - n));
    }

    public static string Trunc(string s, int n) => s.Length <= n ? s : "..." + s[^(n - 3)..];
}
