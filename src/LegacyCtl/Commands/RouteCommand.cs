using System.Text.Json;
using LegacyDemo.Shared;
using Tallgrass.FinalBill;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy route: show or change where each strangled capability sends its traffic.
/// Raising the modern percentage is gated on the last shadow run: you do not get to
/// route customers to code whose differences nobody has explained.
/// </summary>
public static class RouteCommand
{
    public static int Run(Paths paths, CommandLine cli)
    {
        var routesPath = Path.Combine(paths.Modern, "routes.json");
        var routes = RouteTable.Load(routesPath);
        var name = cli.Arg(0);

        if (name is not null && (cli.Has("modern") || cli.Has("mode")))
        {
            if (!routes.TryGetValue(name, out var current)) throw new ArgumentException($"No route named '{name}' in modern/routes.json.");
            var next = current;
            if (cli.Get("mode") is { } mode) next = next with { Mode = Enum.Parse<RouteMode>(mode, ignoreCase: true) };
            if (cli.Has("modern"))
            {
                var pct = Math.Clamp(cli.GetInt("modern", 0), 0, 100);
                next = next with { Mode = pct > 0 ? RouteMode.Modern : RouteMode.Shadow, ModernPercent = pct };
            }

            if (next.ModernPercent > current.ModernPercent && !cli.Has("force"))
            {
                var verdictPath = Path.Combine(paths.Runtime, $"shadow-{name}.json");
                var verdict = File.Exists(verdictPath)
                    ? JsonDocument.Parse(File.ReadAllText(verdictPath)).RootElement.GetProperty("verdict").GetString()
                    : null;
                if (verdict != "GO")
                {
                    Con.Line();
                    Con.Line($"   {Con.Fail("REFUSED")} {name}: {current.ModernPercent}% -> {next.ModernPercent}% modern");
                    Con.Line(verdict is null
                        ? $"            no shadow run on record. Run: legacy shadow"
                        : $"            the last shadow run was {verdict}. Explain or accept every difference first.");
                    Con.Line();
                    Html.Emit(cli.Get("html"), Views.Route(routes, verdict is null
                        ? $"{name}: {current.ModernPercent}% → {next.ModernPercent}% on the new code. There is no shadow run on record."
                        : $"{name}: {current.ModernPercent}% → {next.ModernPercent}% on the new code. The last shadow run was {verdict}. Explain or accept every difference first."));
                    return 1;
                }
            }
            routes[name] = next;
            RouteTable.Save(routesPath, routes);
        }

        Con.Header("legacy route", paths.Rel(routesPath));
        var accounts = Enumerable.Range(0, 1000).Select(i => 1_000_000 + i * 997).ToList();
        foreach (var (key, r) in routes)
        {
            var modern = r.Mode == RouteMode.Modern ? accounts.Count(a => FinalBillRouter.Bucket(a) < r.ModernPercent) : 0;
            var mode = r.Mode switch
            {
                RouteMode.Legacy => Con.C(Con.Grey, "legacy"),
                RouteMode.Shadow => Con.C(Con.Amber, "shadow"),
                _ => Con.C(Con.Green, "modern"),
            };
            var detail = r.Mode switch
            {
                RouteMode.Legacy => "old code only; the door exists, nothing is behind it yet",
                RouteMode.Shadow => "old code answers, new code runs alongside, differences recorded",
                _ => $"{modern} of 1,000 sample accounts on new code (sticky); the rest still shadowed",
            };
            Con.Line($"   {key,-16} {mode,-8} {r.ModernPercent,3}%   {Con.C(Con.Grey, detail)}");
        }
        Con.Line();
        Html.Emit(cli.Get("html"), Views.Route(routes, null));
        return 0;
    }
}
