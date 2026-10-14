using System.Text.RegularExpressions;

namespace LegacyCtl.Estate;

/// <summary>
/// A seam blocker is anything that makes a unit of code impossible to run on its own:
/// it reaches out to a global instead of being handed what it needs. Every one of
/// these is a place a test cannot get between the code and the world.
///
/// These are the same rules `legacy map` counts, `legacy seams` explains, and
/// `legacy ratchet` refuses to let grow.
/// </summary>
public sealed record Rule(string Id, string Label, string Seam, Regex Pattern);

public static class Rules
{
    private static Regex R(string pattern) => new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly IReadOnlyList<Rule> Blockers =
    [
        new("ambient-time",   "reads the wall clock",            "inject TimeProvider",                          R(@"\bDateTime\.(Now|Today|UtcNow)\b")),
        new("static-db",      "opens its own database",          "extract an interface, pass it in",             R(@"\bDb\.Open\s*\(")),
        new("ambient-config", "reads global configuration",      "pass the value (or IOptions<T>) in",           R(@"\bAppSettings\.(Get\w*|ConnectionString)\s*\(|\bConfigurationManager\.")),
        new("singleton",      "reaches for a singleton",         "inject the instance",                          R(@"\b[A-Z]\w+\.Instance\b")),
        new("ambient-http",   "reads the current HTTP request",  "pass the values in (IHttpContextAccessor at the edge)", R(@"\bHttpContext(Shim)?\.Current\b")),
        new("newed-service",  "news up a heavyweight collaborator", "constructor injection",                   R(@"\bnew\s+[A-Z]\w*(Manager|Processor|Generator|Engine|Service)\s*\(\s*\)")),
        new("static-http",    "shares a static HttpClient",      "IHttpClientFactory",                           R(@"\bstatic\s+readonly\s+HttpClient\b")),
        new("sync-over-async","blocks on async (.Result)",       "async all the way down",                       R(@"\)\.Result\b")),
    ];

    public static readonly Regex CompileFork = R(@"^\s*#if\s+!?\s*(\w+)");

    public static bool IsComment(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal)
            || t.StartsWith("/*", StringComparison.Ordinal) || t.StartsWith("--", StringComparison.Ordinal);
    }

    /// <summary>Code part of a line: strips a trailing // comment (good enough; no string-literal parsing).</summary>
    public static string CodeOf(string line)
    {
        var i = line.IndexOf("//", StringComparison.Ordinal);
        if (i < 0) return line;
        // don't cut inside "https://" in a string literal
        var quotes = line[..i].Count(c => c == '"');
        return quotes % 2 == 1 ? line : line[..i];
    }
}

public sealed record Hit(Rule Rule, string File, int Line, string Text)
{
    /// <summary>The line, windowed so the matched pattern is always visible.</summary>
    public string Snippet(int width)
    {
        if (Text.Length <= width) return Text;
        var m = Rule.Pattern.Match(Text);
        var center = m.Success ? m.Index + m.Length / 2 : 0;
        var start = Math.Clamp(center - width / 2, 0, Text.Length - width + 3);
        var s = Text.Substring(start, Math.Min(width - 3, Text.Length - start));
        return (start > 0 ? "..." : "") + s + (start + s.Length < Text.Length ? "..." : "");
    }
}
