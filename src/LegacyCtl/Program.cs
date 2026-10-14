using LegacyCtl;
using LegacyCtl.Commands;
using LegacyDemo.Shared;

// -----------------------------------------------------------------------------
// legacy -- a pragmatic toolkit for a codebase with ten years of history.
//
//   legacy map       read it before you touch it: coupling, god classes, SQL, hotspots
//   legacy seams     why can't this be tested, and what is the smallest fix?
//   legacy shadow    replay production through the new code; explain every difference
//   legacy route     move traffic through the strangler, gated on the shadow verdict
//   legacy triage    modernize, strangle, contain, leave alone, or retire
//   legacy ratchet   the legacy does not get to grow
//
// Plumbing: legacy diff | show | page | step | reset | selftest
// Every report command also takes --html <file> and writes the same report as an HTML
// fragment; `legacy page` stitches a demo's fragments into one page (see demo.ps1).
// -----------------------------------------------------------------------------

var cli = new CommandLine(args.Skip(1));
Con.Init(cli.Has("no-color"));
Redactor.Load(cli.Get("redact") ?? Environment.GetEnvironmentVariable("LEGACY_REDACT"));
var command = args.Length > 0 ? args[0] : "help";

try
{
    var paths = command is "help" or "--help" or "-h" ? null : new Paths { Root = RepoRoot.Find(cli.Get("repo")) };
    return command switch
    {
        "map" => MapCommand.Run(paths!, cli),
        "seams" => SeamsCommand.Run(paths!, cli),
        "shadow" => ShadowCommand.Run(paths!, cli),
        "route" => RouteCommand.Run(paths!, cli),
        "triage" => TriageCommand.Run(paths!, cli),
        "ratchet" => RatchetCommand.Run(paths!, cli),
        "diff" => DiffCommand.Run(paths!, cli),
        "show" => ShowCommand.Run(paths!, cli),
        "page" => PageCommand.Run(paths!, cli),
        "step" => StepCommand.Run(paths!, cli),
        "reset" => StepCommand.Reset(paths!, cli),
        "selftest" => SelfTestCommand.Run(paths!, cli),
        "help" or "--help" or "-h" => Help(0),
        _ => Unknown(command),
    };
}
catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or ArgumentException
                              or InvalidOperationException or System.Text.Json.JsonException or FormatException)
{
    // Expected, actionable failures. A stack trace on a projector teaches nobody anything.
    Con.Line();
    Con.Line(Con.C(Con.Red, "error: ") + ex.Message);
    Con.Line();
    return 2;
}

static int Unknown(string command)
{
    Con.Line();
    Con.Line(Con.C(Con.Red, $"Unknown command '{command}'."));
    return Help(2);
}

static int Help(int exitCode)
{
    Con.Line();
    Con.Line(Con.C(Con.Bold, "legacy") + " - a pragmatic toolkit for a codebase with ten years of history");
    Con.Line();
    Con.Line("  map                 coupling, god classes, logic in SQL, hotspots, where to start");
    Con.Line("                      --estate <dir>    any folder of .csproj/.cs/.sql (git history if present)");
    Con.Line("                      --mermaid <file>  also write the dependency graph");
    Con.Line("  seams <file.cs>     what blocks testing, and the smallest seam that fixes it");
    Con.Line("                      --method <Name>   narrow to one method");
    Con.Line("  shadow              replay recorded final-bill traffic through the modern code");
    Con.Line("                      --policy <json>  --cases <csv>  --known <json>");
    Con.Line("  route [name]        show routes; --modern <pct> | --mode legacy|shadow|modern");
    Con.Line("  triage              modernize / strangle / contain / leave alone / retire");
    Con.Line("  ratchet             fail if any seam blocker count grew; --update to tighten");
    Con.Line();
    Con.Line("  diff <a> <b>        coloured unified diff (no git needed)");
    Con.Line("  show <file>         numbered lines; --from N --to N --highlight 3,5");
    Con.Line("  page                stitch HTML fragments into a page; --dir --out --title");
    Con.Line("  step <name>         apply a pre-written demo step from steps/");
    Con.Line("  reset               restore the day-one state");
    Con.Line("  selftest            assert every demo path");
    Con.Line();
    Con.Line(Con.C(Con.Grey, "  --no-color  --repo <path>  --html <fragment.html>  --redact <names.txt>"));
    Con.Line();
    return exitCode;
}
