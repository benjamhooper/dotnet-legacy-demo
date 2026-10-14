using LegacyCtl.Estate;
using LegacyDemo.Shared;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy triage: when to modernize, and when to leave well enough alone.
///
/// Ugliness is not on the list of inputs. Change, pain, coupling, and whether anyone
/// still uses it are. The code that looks worst is frequently the code that should be
/// left exactly where it is, with a fence around it.
/// </summary>
public static class TriageCommand
{
    public sealed record Verdict(string Subject, string Call, string Why, int Commits, int Incidents, int Radius, int Blockers, bool God);

    public static readonly string[] Order = ["MODERNIZE", "STRANGLE", "KEEP GOING", "CONTAIN", "LEAVE ALONE", "RETIRE LAST", "RETIRE", "WATCH"];

    public static int Run(Paths paths, CommandLine cli)
    {
        var model = EstateModel.Load(paths.Estate);
        var signals = Signals.Load(paths.Estate);
        var verdicts = Evaluate(model, signals);

        Con.Header("legacy triage", "change x pain x coupling x use   (estate/.history/signals.json)");
        Con.Line(Con.C(Con.Grey, "   subject                        commits  incidents  blast  blockers   call"));
        foreach (var v in verdicts.OrderBy(v => Array.IndexOf(Order, v.Call)).ThenByDescending(v => v.Commits))
        {
            Con.Line($"   {MapCommand.Short(v.Subject),-30} {v.Commits,6} {v.Incidents,9} {v.Radius,6} {v.Blockers,8}   {Mark(v.Call)}");
            Con.Note(v.Why, 6);
        }
        Con.Line();
        var counts = Order.Select(o => (o, verdicts.Count(v => v.Call == o))).Where(t => t.Item2 > 0);
        Con.Line("   " + string.Join("   ", counts.Select(t => $"{t.o.ToLowerInvariant()} {t.Item2}")));
        var alone = verdicts.Where(v => v.Call is "LEAVE ALONE" or "CONTAIN").ToList();
        var ugliest = model.Files.OrderByDescending(f => f.CodeLines).First();
        Con.Line();
        Con.Note($"{alone.Count} of {verdicts.Count} get no modernization budget at all. The largest file in the estate, " +
                 $"{ugliest.RelPath} ({Con.N(ugliest.CodeLines)} lines), is in that group. Stable, correct, and ugly is a fine place for code to be.", 3);
        Con.Line();
        Html.Emit(cli.Get("html"), Views.Triage(verdicts, model));
        return 0;
    }

    public static List<Verdict> Evaluate(EstateModel model, Signals signals)
    {
        var total = model.Projects.Count;
        var list = new List<Verdict>();
        foreach (var p in model.Projects)
        {
            var s = signals.For(p.Name);
            var god = p.Files.Any(f => f.IsGodClass);
            var radius = p.Dependents.Count;
            string call, why;

            if (!string.IsNullOrEmpty(s.ReplacedBy) || s.Usage.StartsWith("0 ", StringComparison.Ordinal))
            {
                (call, why) = ("RETIRE", $"{s.Usage}. Replaced by {s.ReplacedBy ?? "something else"}. Check the logs, then delete it; do not modernize it.");
            }
            else if (s.Usage.Contains("repair", StringComparison.OrdinalIgnoreCase))
            {
                (call, why) = ("RETIRE LAST", $"{s.Usage}. Every button is a bug upstream. Retire it by making the repairs unnecessary; track repairs/month to zero.");
            }
            else if (radius >= total / 2)
            {
                (call, why) = ("CONTAIN", $"blast radius {radius}. No new dependencies on it; move things out of it, never into it; ratchet its blockers.");
            }
            else if ((p.Commits >= 30 || s.Roadmap.Length > 0) && (god || p.BlockersPer100 >= 3) && s.Incidents12mo >= 3)
            {
                (call, why) = ("MODERNIZE", $"{p.Commits} commits, {s.Incidents12mo} incidents, on the roadmap ({s.Roadmap}). You will be in here anyway: seam it, pin it, move it a method at a time.");
            }
            else if ((p.Commits >= 20 || s.Roadmap.Length > 0) && p.BlockersPer100 < 2)
            {
                (call, why) = ("KEEP GOING", $"changes constantly and already has seams ({p.Blockers} blockers). This is what the rest should look like. Normal investment.");
            }
            else if (p.Commits <= 5 && s.Incidents12mo == 0)
            {
                (call, why) = ("LEAVE ALONE", $"{p.Commits} commits and 0 incidents in a year{(god ? ", god class and all" : "")}. Put a fence around it (ratchet) and spend the money elsewhere.");
            }
            else
            {
                (call, why) = ("WATCH", $"{Plural(s.Incidents12mo, "incident")}, {p.Commits} commits, nothing on the roadmap. Revisit when it shows up there.");
            }
            if (p.IsNetFramework && call.StartsWith("RETIRE", StringComparison.Ordinal))
                why += $" Still on .NET Framework ({p.Framework}): porting it would be modernizing something you are about to delete.";
            list.Add(new Verdict(p.Name, call, why, p.Commits, s.Incidents12mo, radius, p.Blockers, god));
        }

        foreach (var proc in model.Procedures.Where(x => x.HasBusinessLogic))
        {
            signals.Procedures.TryGetValue(proc.Name, out var s);
            if (s is null || (s.Roadmap.Length == 0 && proc.Commits < 10)) continue;
            list.Add(new Verdict(proc.Name, "STRANGLE",
                $"{proc.Decisions} decisions in SQL, {proc.Commits} changes, {s.Incidents12mo} incidents, called from {proc.CalledFrom.Count} projects. One door in front, shadow, then switch.",
                proc.Commits, s.Incidents12mo, proc.CalledFrom.Count, 0, false));
        }
        return list;
    }

    private static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";

    private static string Mark(string call) => call switch
    {
        "MODERNIZE" or "STRANGLE" => Con.Warn(call),
        "KEEP GOING" => Con.Pass(call),
        "LEAVE ALONE" => Con.Info(call),
        "CONTAIN" => Con.Fail(call),
        "RETIRE" or "RETIRE LAST" => Con.C(Con.Grey, "[" + call + "]"),
        _ => "[" + call + "]",
    };
}
