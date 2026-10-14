using System.Globalization;
using System.Text;
using LegacyCtl.Commands;
using LegacyCtl.Estate;
using LegacyDemo.Shared;
using Tallgrass.FinalBill;
using static LegacyDemo.Shared.Html;

namespace LegacyCtl;

/// <summary>
/// The HTML side of every command. Each method returns one fragment; the console output
/// stays exactly as it was, and these are written only when a command gets --html.
/// </summary>
public static class Views
{
    private static string Short(string p) => MapCommand.Short(p);

    // -----------------------------------------------------------------------------------
    // legacy map
    // -----------------------------------------------------------------------------------
    public static string Map(EstateModel model, bool narrate = true)
    {
        var total = model.Projects.Count;
        var files = model.Files.ToList();
        var hubs = model.Projects.Where(p => p.Dependents.Count >= total / 2).ToList();
        var sb = new StringBuilder();

        // 0. At a glance + graph
        var glance = Tiles(
            ("projects", N(total), Tone.Info),
            ("C# files", N(files.Count), Tone.Info),
            ("lines of C#", N(files.Sum(f => f.CodeLines)), Tone.Info),
            narrate ? ("stored procedures", N(model.Procedures.Count), Tone.Info) : ("test methods", N(model.TestMethods), model.TestMethods == 0 ? Tone.Bad : Tone.Info),
            ("hub projects", N(hubs.Count), Tone.Bad));
        sb.Append(Section("legacy map" + (narrate || model.History is null ? "" : " · " + model.History), "The codebase at a glance",
            glance + DependencyGraph(model) + (narrate ? "" : ChainNote(model)) +
            "<div class=\"legend\"><span><i class=\"sw bad\"></i>hub (most of the codebase depends on it)</span>" +
            "<span><i class=\"sw warn\"></i>still on .NET Framework</span><span><i class=\"sw good\"></i>start here</span>" +
            "<span>arrows point from a project to what it depends on</span></div>",
            "Every project, and what it depends on. Read right to left: the leaves on the right depend on everything to their left."));

        // 1. Dependencies
        var rows = model.Projects.OrderByDescending(p => p.Dependents.Count).ThenBy(p => p.Name).Select(p =>
        {
            var radius = p.Dependents.Count;
            var tags = new List<string>();
            if (radius >= total / 2) tags.Add(Badge("HUB", Tone.Bad));
            else if (radius == 0) tags.Add(Badge("leaf", Tone.Muted));
            if (p.IsNetFramework) tags.Add(Badge(p.Framework, Tone.Warn));
            return new[]
            {
                $"<b>{E(Short(p.Name))}</b>",
                Bar(radius, total - 1, radius >= total / 2 ? Tone.Bad : Tone.Info, $"{radius} of {total - 1}",
                    $"{p.Name}: a change here can break {radius} other projects"),
                $"<span class=\"mono\">{p.ReferencedBy.Count}</span>",
                $"<span class=\"mono\">{p.References.Count}</span>",
                $"<span class=\"mono\">{N(p.Lines)}</span>",
                $"<span class=\"mono\">{p.Blockers}</span>",
                string.Join(" ", tags),
            };
        });
        var fw = string.Concat(model.Projects.GroupBy(p => p.Framework).OrderByDescending(g => g.Count())
            .Select(g => Chip($"{g.Key} × {g.Count()}")));
        var oldStyle = model.Projects.Where(p => !p.SdkStyle).Select(p => Short(p.Name)).ToList();
        var dlls = model.Projects.SelectMany(p => p.BinaryReferences.Select(b => Path.GetFileName(b))).Distinct().ToList();
        var pages = model.Projects.Sum(p => p.WebFormsPages);
        var factCards = new List<string>
        {
            MiniCard(Badge("FRAMEWORKS", Tone.Info), string.Join(", ", model.Projects.GroupBy(p => p.Framework).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} on {g.Key}")),
                $"{string.Join(", ", model.Projects.GroupBy(p => p.Framework).Select(g => $"{g.Count()} on {g.Key}"))}." +
                (oldStyle.Count > 0 ? $" Old-style .csproj: {string.Join(", ", oldStyle.Take(6))}{(oldStyle.Count > 6 ? "..." : "")}." : "")),
        };
        if (dlls.Count > 0 || narrate)
            factCards.Add(MiniCard(Badge("LIB", Tone.Warn), $"{dlls.Count} DLLs referenced by HintPath",
                "No package, no source, no visible upgrade path.", string.Join("  ·  ", dlls.Take(6))));
        if (pages > 0 || narrate)
            factCards.Add(MiniCard(Badge("WEB FORMS", Tone.Warn), $"{pages} .aspx pages",
                "System.Web has no in-place upgrade to ASP.NET Core. Only a strangler."));
        if (!narrate)
        {
            foreach (var (sln, listed, missing) in model.Solutions)
                factCards.Add(MiniCard(missing > 0 ? Badge("SOLUTION", Tone.Bad) : Badge("SOLUTION", Tone.Info), $"{Path.GetFileName(sln)}: {listed} projects, {listed - missing} exist",
                    missing == 0 ? $"Lists {listed} projects; all of them exist."
                                 : missing == listed ? $"Lists {listed} projects. None of them exist. Which solution does the build server open?"
                                                     : $"Lists {listed} projects; {missing} of them do not exist.", sln));
            var lines = files.Sum(f => f.CodeLines);
            factCards.Add(MiniCard(Badge("TESTS", model.TestMethods == 0 ? Tone.Bad : Tone.Info), $"{N(model.TestMethods)} test methods",
                $"In {model.TestProjects} test project{(model.TestProjects == 1 ? "" : "s")}, against {N(lines)} lines of C#" +
                (model.TestMethods == 0 ? "." : $": one test per {N(lines / model.TestMethods)} lines.")));
        }
        var facts = Grid(factCards, 3);
        var top = hubs.OrderByDescending(h => h.Dependents.Count).FirstOrDefault();
        sb.Append(Section("1. Dependencies", "Who breaks if this changes?",
            Table(new[] { "project", "blast radius", "refs in", "refs out", "lines", "blockers", "" }, rows) + facts +
            (top is null ? "" : Callout($"{Short(top.Name)} can break {top.Dependents.Count} of the other {total - 1} projects.", Tone.Bad))));

        // 2. God classes
        var allGods = files.Where(f => f.IsGodClass).ToList();
        var gods = allGods.OrderByDescending(f => f.CodeLines).Take(narrate ? allGods.Count : 8).Select(f =>
            MiniCard(Badge("GOD CLASS", Tone.Warn), $"{Path.GetFileNameWithoutExtension(f.RelPath)} · {N(f.CodeLines)} lines · {f.Commits} commits",
                $"{N(f.CodeLines)} lines · {f.PublicMethods} public methods · reaches {f.NamespacesReached} namespaces",
                $"{f.Commits} commits in the last 12 months  ·  {f.RelPath}"));
        sb.Append(Section("2. God classes", narrate ? "Too big to hold in your head" : $"Too big to hold in your head: {allGods.Count} of them" + (allGods.Count > 8 ? ", the 8 largest" : ""), Grid(gods, 2) +
            (narrate ? P("Same size, very different stories: look at the commit counts. That asymmetry is demo four.", "note")
                     : P("500+ lines, 25+ public methods, or 10+ constructor parameters. Now look at the commit counts: size alone does not tell you where to start.", "note"))));

        // 3. Logic in SQL
        var maxDec = Math.Max(1, model.Procedures.Select(p => p.Decisions).DefaultIfEmpty(0).Max());
        var procRows = model.Procedures.OrderByDescending(p => p.Decisions).Select(p => new[]
        {
            $"<span class=\"mono\">{E(p.Name)}</span>",
            Bar(p.Decisions, maxDec, p.HasBusinessLogic ? Tone.Warn : Tone.Muted, p.Decisions.ToString(CultureInfo.InvariantCulture)),
            p.HasBusinessLogic ? Badge("RULES", Tone.Warn) : Badge(narrate ? "crud" : "script", Tone.Muted),
            p.CalledFrom.Count == 0
                ? Badge("called from nothing in this repo", Tone.Bad)
                : string.Concat(p.CalledFrom.Select(c => Chip(Short(c)))),
        });
        var ruleCount = model.Procedures.Count(p => p.HasBusinessLogic);
        sb.Append(Section("3. Logic in SQL", "Business rules the C# cannot see",
            model.Procedures.Count == 0
            ? Callout("No .sql files in source control.", Tone.Warn)
            : !narrate && ruleCount == 0
            ? Table(new[] { "sql file", "decisions", "", "called from" }, procRows) +
              Callout($"{model.Procedures.Count} .sql scripts, none referenced from the C#.", Tone.Warn)
            : Table(new[] { "procedure", "decisions", "", "called from" }, procRows) +
            Callout($"{ruleCount} of {model.Procedures.Count} procedures hold business rules the C# cannot see.", Tone.Warn)));

        // 4. Hotspots
        var hot = files.Where(f => f.Commits > 0)
            .Select(f => (f.RelPath, f.Commits, f.CodeLines, Score: f.Commits * Math.Log10(Math.Max(10, f.CodeLines))))
            .Concat(model.Procedures.Where(p => p.Commits > 0)
                .Select(p => (p.RelPath, p.Commits, p.CodeLines, Score: p.Commits * Math.Log10(Math.Max(10, p.CodeLines)))))
            .OrderByDescending(x => x.Score).Take(6).ToList();
        var maxScore = hot.Count == 0 ? 1 : hot.Max(h => h.Score);
        var hotRows = hot.Select((h, i) => new[]
        {
            $"<span class=\"mono\">{E(h.RelPath)}</span>",
            Bar(h.Score, maxScore, i == 0 ? Tone.Bad : Tone.Info, $"{h.Commits} commits", $"{h.Commits} commits × {h.CodeLines} lines"),
            $"<span class=\"mono\">{N(h.CodeLines)}</span>",
        });
        var forks = files.SelectMany(f => f.Forks.Select(x => (x.Symbol, f.RelPath))).GroupBy(x => x.Symbol)
            .Select(g => MiniCard(Badge("FORK", Tone.Warn), $"#if {g.Key} · {g.Count()} blocks",
                $"{g.Count()} blocks in {g.Select(x => x.RelPath).Distinct().Count()} files. Two programs share this source tree; CI builds one of them."));
        sb.Append(Section("4. Hotspots", "Change × size, last 12 months",
            (hot.Count == 0 ? Callout("No change history: no .history/churn-12mo.tsv and no git log.", Tone.Muted)
                            : Table(new[] { "file", "hotspot score", "lines" }, hotRows)) + Grid(forks, 2),
            narrate || model.History is null ? null : model.History + ". Score = commits × log(lines)."));

        // 5. Where to start
        var cards = new List<string>();
        foreach (var p in model.Projects.Where(p => !p.IsTest && p.Dependents.Count <= 1 && p.Commits >= 20 && p.BlockersPer100 < 2))
            cards.Add(MiniCard(Badge("START HERE", Tone.Good), Short(p.Name),
                $"Changes every sprint ({p.Commits} commits), nothing depends on it, and it already has seams ({p.Blockers} blockers). A cheap, visible win."));
        foreach (var f in files.Where(f => f.IsGodClass && f.Commits >= (narrate ? 20 : 10)).OrderByDescending(f => f.Commits).Take(3))
            cards.Add(MiniCard(Badge("SEAM FIRST", Tone.Warn), Path.GetFileNameWithoutExtension(f.RelPath),
                (narrate ? "The hottest file and a god class." : $"A hotspot ({f.Commits} commits) and a god class ({N(f.CodeLines)} lines).") +
                " Don't rewrite it: cut one seam at its edge, pin its behavior, move one method at a time.", narrate ? "demo 2" : f.RelPath));
        foreach (var proc in model.Procedures.Where(p => p.HasBusinessLogic && p.Commits >= 10 && p.CalledFrom.Count >= 2))
            cards.Add(MiniCard(Badge("STRANGLE", Tone.Warn), proc.Name,
                $"{proc.Decisions} business decisions, {proc.Commits} changes this year, called from {proc.CalledFrom.Count} projects. One door in front, then replace what's behind it.", narrate ? "demo 3" : null));
        if (hubs.Count > 0)
            cards.Add(MiniCard(Badge("NOT HERE", Tone.Bad),
                string.Join(", ", hubs.OrderByDescending(h => h.Dependents.Count).Take(narrate ? hubs.Count : 4).Select(h => Short(h.Name))) + (hubs.Count > 4 && !narrate ? $" and {hubs.Count - 4} more" : ""),
                "The hubs. Blast radius of most of the codebase. Contain them; don't start in them.", narrate ? "demo 4" : null));
        sb.Append(Section("5. Where to start", narrate ? "Four verdicts, one per remaining demo" : "Where to start, and where not to", Grid(cards, 2)));
        return sb.ToString();
    }

    /// <summary>The longest chain of project references, bottom of the stack first.</summary>
    private static string ChainNote(EstateModel model)
    {
        var memo = new Dictionary<string, List<string>>();
        List<string> Longest(Project p)
        {
            if (memo.TryGetValue(p.Name, out var m)) return m;
            memo[p.Name] = new() { p.Name };   // cycle guard
            var best = p.References.Select(model.Find).Where(r => r is not null).Select(r => Longest(r!))
                        .OrderByDescending(c => c.Count).FirstOrDefault() ?? new List<string>();
            return memo[p.Name] = best.Append(p.Name).ToList();
        }
        var chain = model.Projects.Select(Longest).OrderByDescending(c => c.Count).FirstOrDefault();
        if (chain is null || chain.Count < 5) return "";
        return Callout($"A chain {chain.Count} projects deep: {string.Join(" → ", chain.Select(Short))}", Tone.Warn);
    }

    /// <summary>Layered dependency graph: column = depth from the bottom of the stack.</summary>
    private static string DependencyGraph(EstateModel model)
    {
        var level = new Dictionary<string, int>();
        int Level(Project p)
        {
            if (level.TryGetValue(p.Name, out var l)) return l;
            level[p.Name] = 0;
            var refs = p.References.Select(model.Find).Where(r => r is not null).ToList();
            var v = refs.Count == 0 ? 0 : refs.Max(r => Level(r!)) + 1;
            level[p.Name] = v;
            return v;
        }
        foreach (var p in model.Projects) Level(p);

        var total = model.Projects.Count;
        var cols = level.Values.Max() + 1;
        const int colW = 196, boxW = 168, boxH = 34, rowH = 50, padX = 10, padY = 30;
        var byCol = model.Projects.GroupBy(p => level[p.Name]).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Name).ToList());
        var maxRows = byCol.Values.Max(v => v.Count);
        var width = padX * 2 + (cols - 1) * colW + boxW;
        var height = padY + maxRows * rowH + 10;
        var pos = new Dictionary<string, (double X, double Y)>();
        foreach (var (c, list) in byCol)
        {
            var offset = (maxRows - list.Count) * rowH / 2.0;
            for (var i = 0; i < list.Count; i++)
                pos[list[i].Name] = (padX + c * colW, padY + offset + i * rowH);
        }

        var sb = new StringBuilder($"<svg class=\"graph\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"Project dependency graph\">");
        for (var c = 0; c < cols; c++)
            sb.Append($"<text class=\"lvl\" x=\"{padX + c * colW}\" y=\"16\">{(c == 0 ? "depends on nothing" : $"layer {c}")}</text>");
        foreach (var p in model.Projects)
            foreach (var r in p.References)
            {
                if (!pos.ContainsKey(r)) continue;
                var (x1, y1) = pos[p.Name]; var (x2, y2) = pos[r];
                var hub = model.Find(r)!.Dependents.Count >= total / 2;
                double sx = x1, sy = y1 + boxH / 2.0, ex = x2 + boxW, ey = y2 + boxH / 2.0, mx = (sx + ex) / 2;
                sb.Append(FormattableString.Invariant(
                    $"<path class=\"edge{(hub ? " hot" : "")}\" d=\"M{sx:0},{sy:0} C{mx:0},{sy:0} {mx:0},{ey:0} {ex:0},{ey:0}\"/>"));
            }
        foreach (var p in model.Projects)
        {
            var (x, y) = pos[p.Name];
            var cls = p.Dependents.Count >= total / 2 ? "hub" : p.IsNetFramework ? "fw"
                    : !p.IsTest && p.Dependents.Count <= 1 && p.Commits >= 20 && p.BlockersPer100 < 2 ? "start" : "";
            var tip = $"{p.Name}: {p.References.Count} refs out, {p.ReferencedBy.Count} refs in, blast radius {p.Dependents.Count}, {p.Framework}";
            sb.Append(FormattableString.Invariant(
                $"<g><title>{E(tip)}</title><rect class=\"nodebox {cls}\" x=\"{x:0}\" y=\"{y:0}\" width=\"{boxW}\" height=\"{boxH}\" rx=\"7\"/>"));
            sb.Append(FormattableString.Invariant(
                $"<text x=\"{x + 10:0}\" y=\"{y + 21:0}\">{E(Short(p.Name))}</text></g>"));
        }
        return sb.Append("</svg>").ToString();
    }

    // -----------------------------------------------------------------------------------
    // legacy shadow
    // -----------------------------------------------------------------------------------
    public static string Shadow(ShadowCommand.Outcome o, ProrationPolicy policy, (string Label, string Desc)[] behaviors)
    {
        var differ = o.Total - o.Matched;
        var explainedCount = o.Explained.Values.Sum(v => v.Count);
        var accepted = o.Rejected.Count(r => r.Known is not null);
        var unexplained = o.Unexplained + o.Rejected.Count(r => r.Known is null);
        var sb = new StringBuilder();
        sb.Append(Tiles(
            ("identical to production", N(o.Matched), o.Go ? Tone.Good : Tone.Info),
            ("different", N(differ), differ == 0 ? Tone.Good : o.Go ? Tone.Info : Tone.Bad),
            ("explained or accepted", N(explainedCount + accepted), Tone.Warn),
            ("unexplained", N(unexplained), unexplained == 0 ? Tone.Good : Tone.Bad)));
        sb.Append(Stack(
            ("identical", o.Matched, Tone.Good),
            ("explained by a legacy behavior", explainedCount, Tone.Warn),
            ("accepted, with an owner", accepted, Tone.Info),
            ("unexplained", unexplained, Tone.Bad)));
        if (o.NetDrift != 0)
            sb.Append(P($"If shipped as-is, the new code would have billed ${M(Math.Abs(o.NetDrift))} {(o.NetDrift < 0 ? "less" : "more")} across these 30 days.", "note"));
        sb.Append($"<p class=\"note\">Policy: {string.Concat(policy.Describe().Split("  ", StringSplitOptions.RemoveEmptyEntries).Select(x => Chip(x.Trim())))}</p>");

        if (o.Explained.Count > 0)
        {
            sb.Append(H3("Explained by a legacy behavior"));
            var rows = new List<string[]>();
            foreach (var (label, desc) in behaviors)
            {
                var touching = o.Explained.Where(kv => kv.Key.Split(" + ").Contains(label)).SelectMany(kv => kv.Value).ToList();
                if (touching.Count == 0) continue;
                var ex = o.Explained.TryGetValue(label, out var solo) && solo.Count > 0 ? solo[0] : touching[0];
                rows.Add(new[]
                {
                    Badge("QUIRK", Tone.Warn) + $" <b>{E(desc)}</b><br><span class=\"mono\" style=\"color:var(--muted)\">{E(label)}</span>",
                    Bar(touching.Count, o.Total, Tone.Warn, $"{N(touching.Count)} requests"),
                    $"<span class=\"mono\">{E(ex.Case.Id)}</span> {E(ex.Detail)}",
                });
            }
            sb.Append(Table(new[] { "behavior", "requests it explains", "example" }, rows));
            var multi = o.Explained.Where(kv => kv.Key.Contains(" + ")).Sum(kv => kv.Value.Count);
            if (multi > 0) sb.Append(P($"{N(multi)} requests only match once two of these are switched on together.", "note"));
        }
        if (o.Rejected.Count > 0)
        {
            sb.Append(H3("The new code refused what the old code accepted"));
            foreach (var g in o.Rejected.GroupBy(r => r.Known is not null))
            {
                var first = g.First();
                var k = first.Known;
                sb.Append(MiniCard(g.Key ? Badge("ACCEPTED", Tone.Info) : Badge("UNEXPLAINED", Tone.Bad),
                    $"{g.Count()} requests: {first.Case.Id} and similar", first.Detail,
                    k is null ? "needs an owner" : $"owner {k.Owner} · ticket {k.Ticket} · expires {k.Expires} · \"{k.Reason}\""));
            }
        }
        sb.Append(o.Go
            ? Verdict("GO", "Every difference is explained or explicitly accepted. Safe to route a slice of traffic.", Tone.Good)
            : Verdict("NO-GO", $"{N(o.Blocking)} requests would bill differently. Nothing routes to the new code until each one is a decision: adopt the legacy behavior in the policy, or accept the difference with an owner and an expiry.", Tone.Bad));
        if (!o.Go && o.Explained.Count > 0)
            sb.Append(Callout("The new code is not wrong. Production is the spec now.", Tone.Warn));
        return Section("legacy shadow · final-bill", $"{N(o.Total)} production requests, replayed through the new code", sb.ToString(),
            "Legacy = what dbo.usp_ProrateFinalBill returned in production. Modern = FinalBillCalculator, written from the tariff.");
    }

    // -----------------------------------------------------------------------------------
    // legacy route
    // -----------------------------------------------------------------------------------
    public static string Route(Dictionary<string, Tallgrass.FinalBill.Route> routes, string? refused)
    {
        var accounts = Enumerable.Range(0, 1000).Select(i => 1_000_000 + i * 997).ToList();
        var rows = routes.Select(kv =>
        {
            var r = kv.Value;
            var modern = r.Mode == RouteMode.Modern ? accounts.Count(a => FinalBillRouter.Bucket(a) < r.ModernPercent) : 0;
            var badge = r.Mode switch
            {
                RouteMode.Legacy => Badge("legacy", Tone.Muted),
                RouteMode.Shadow => Badge("shadow", Tone.Warn),
                _ => Badge("modern", Tone.Good),
            };
            var detail = r.Mode switch
            {
                RouteMode.Legacy => "Old code only. The door exists; nothing is behind it yet.",
                RouteMode.Shadow => "Old code answers; new code runs alongside; every difference is recorded.",
                _ => $"{modern} of 1,000 sample accounts on the new code (sticky per account). The rest keep shadowing.",
            };
            return new[]
            {
                $"<b class=\"mono\">{E(kv.Key)}</b>", badge,
                Bar(r.ModernPercent, 100, r.ModernPercent > 0 ? Tone.Good : Tone.Muted, $"{r.ModernPercent}% on modern"),
            };
        });
        var body = Table(new[] { "capability", "mode", "traffic on the new code" }, rows);
        if (refused is not null) body = Verdict("REFUSED", refused, Tone.Bad) + body;
        return Section("legacy route", refused is null ? "Where each capability sends its traffic" : "The gate: no GO, no customers", body,
            "modern/routes.json: one door per capability. Raising the percentage is gated on the last shadow verdict.");
    }

    // -----------------------------------------------------------------------------------
    // legacy triage
    // -----------------------------------------------------------------------------------
    public static string Triage(List<TriageCommand.Verdict> verdicts, EstateModel model)
    {
        Tone ToneOf(string call) => call switch
        {
            "MODERNIZE" or "STRANGLE" => Tone.Warn,
            "KEEP GOING" => Tone.Good,
            "LEAVE ALONE" => Tone.Info,
            "CONTAIN" => Tone.Bad,
            _ => Tone.Muted,
        };
        var counts = TriageCommand.Order.Select(o => (o, verdicts.Count(v => v.Call == o))).Where(t => t.Item2 > 0).ToList();
        var tiles = Tiles(counts.Select(t => (t.o.ToLowerInvariant(), t.Item2.ToString(CultureInfo.InvariantCulture), ToneOf(t.o))).ToArray());
        var rows = verdicts.OrderBy(v => Array.IndexOf(TriageCommand.Order, v.Call)).ThenByDescending(v => v.Commits).Select(v => new[]
        {
            $"<b>{E(Short(v.Subject))}</b>",
            Badge(v.Call, ToneOf(v.Call)),
            $"<span class=\"mono\">{v.Commits}</span>",
            $"<span class=\"mono\">{v.Incidents}</span>",
            $"<span class=\"mono\">{v.Radius}</span>",
            $"<span class=\"mono\">{v.Blockers}</span>",
        });
        var alone = verdicts.Count(v => v.Call is "LEAVE ALONE" or "CONTAIN");
        var ugliest = model.Files.OrderByDescending(f => f.CodeLines).First();
        return Section("legacy triage", "Modernize, strangle, contain, leave alone, or retire",
            tiles + Table(new[] { "subject", "call", "commits", "incidents", "blast", "blockers" }, rows) +
            Callout($"{alone} of {verdicts.Count} get no modernization budget. Including the largest file.", Tone.Info),
            "Inputs: change, pain, coupling and use (estate/.history/signals.json). Not on the list: how ugly it is.");
    }

    // -----------------------------------------------------------------------------------
    // legacy ratchet
    // -----------------------------------------------------------------------------------
    public static string Ratchet(Dictionary<string, Dictionary<string, int>> now, RatchetCommand.Baseline baseline,
                                 List<(string Rule, string File, int Was, int Now)> grew, EstateModel model)
    {
        var rows = Rules.Blockers.Select(rule =>
        {
            var was = baseline.Rules.TryGetValue(rule.Id, out var bf) ? bf.Values.Sum() : 0;
            var cur = now[rule.Id].Values.Sum();
            var badge = cur > was ? Badge($"+{cur - was}", Tone.Bad) : cur < was ? Badge($"{cur - was}", Tone.Good) : Badge("ok", Tone.Muted);
            return new[]
            {
                $"<span class=\"mono\">{E(rule.Id)}</span>", E(rule.Label),
                $"<span class=\"mono\">{was}</span>", $"<span class=\"mono\">{cur}</span>", badge,
            };
        });
        var sb = new StringBuilder(Table(new[] { "rule", "what it catches", "baseline", "now", "" }, rows));
        if (grew.Count > 0)
        {
            var added = grew.Sum(g => g.Now - g.Was);
            sb.Append(Verdict("FAIL", $"This change adds {added} new seam blocker{(added == 1 ? "" : "s")}. New code does not get to be legacy code.", Tone.Bad));
            var notes = new List<string>();
            foreach (var g in grew)
            {
                var file = model.Files.First(f => f.RelPath == g.File);
                foreach (var h in file.Hits.Where(h => h.Rule.Id == g.Rule).Take(Math.Max(1, g.Now - g.Was)))
                    notes.Add(MiniCard(Badge(h.Rule.Id, Tone.Bad), $"{Path.GetFileName(h.File)}:{h.Line}", h.Rule.Label + " → " + h.Rule.Seam, h.Text));
            }
            sb.Append(Grid(notes, 3));
        }
        else
        {
            sb.Append(Verdict("PASS", "No new seam blockers. The legacy patterns may go down; they never go up.", Tone.Good));
        }
        return Section("legacy ratchet", "Seam blockers may go down, never up", sb.ToString(),
            "Counted per rule and per file against ratchet.baseline.json, committed to the repo.");
    }

    // -----------------------------------------------------------------------------------
    // small ones
    // -----------------------------------------------------------------------------------
    public static string Step(string name, List<string> changed, string? note)
    {
        var rows = changed.Select(c =>
        {
            var kind = c[..1];
            var b = kind switch { "A" => Badge("added", Tone.Good), "D" => Badge("deleted", Tone.Bad), _ => Badge("modified", Tone.Info) };
            return new[] { b, $"<span class=\"mono\">{E(c[2..])}</span>" };
        });
        return Section("legacy step", "Step " + name + ": " + (changed.Count == 1 ? "1 file" : $"{changed.Count} files") + " changed",
            Table(new[] { "change", "file" }, rows),
            "A pre-written step from steps/, copied into place. Nothing is typed on stage.");
    }

    public static string Diff(string beforeRel, string afterRel, List<(char Kind, string Text, int LineB)> ops, int context, int added, int removed)
    {
        var show = new bool[ops.Count];
        for (var i = 0; i < ops.Count; i++)
            if (ops[i].Kind != ' ')
                for (var k = Math.Max(0, i - context); k <= Math.Min(ops.Count - 1, i + context); k++) show[k] = true;
        var lines = new List<(int?, string, string)>();
        var gap = false;
        for (var i = 0; i < ops.Count; i++)
        {
            if (!show[i]) { gap = true; continue; }
            if (gap) { lines.Add((null, $"@@ line {ops[i].LineB} @@", "hunk")); gap = false; }
            var (kind, text, lineB) = ops[i];
            lines.Add((kind == '-' ? null : lineB, (kind == ' ' ? " " : kind.ToString()) + text, kind switch { '+' => "add", '-' => "del", _ => "" }));
        }
        return Section("legacy diff", Path.GetFileName(afterRel),
            Tiles(("lines added", added.ToString(CultureInfo.InvariantCulture), Tone.Good), ("lines removed", removed.ToString(CultureInfo.InvariantCulture), Tone.Bad)) +
            Code(lines, $"{beforeRel}  →  {afterRel}"));
    }

    public static string Show(string rel, string[] all, int from, int to, HashSet<int> hot, string? title)
    {
        var lines = new List<(int?, string, string)>();
        for (var i = from; i <= Math.Min(to, all.Length); i++)
            lines.Add((i, all[i - 1], hot.Contains(i) ? "hot" : all[i - 1].TrimStart().StartsWith("--", StringComparison.Ordinal) ? "dim" : ""));
        return Section("source", title ?? Path.GetFileName(rel), Code(lines, rel));
    }
}
