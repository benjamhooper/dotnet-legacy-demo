using System.Runtime.InteropServices;
using System.Text;

namespace LegacyDemo.Shared;

/// <summary>
/// Console output that reads the same on Windows Terminal, conhost, macOS, Linux and a
/// CI log with no TTY. Severity is always carried by an ASCII marker ([FAIL], [PASS],
/// [HUB]) as well as colour, because projectors wash colour out and roughly one person
/// in twelve in the room cannot rely on red-versus-green.
/// </summary>
public static class Con
{
    private static bool _color;
    private static bool _initialized;

    public const string Reset = "\u001b[0m";
    public const string Bold = "\u001b[1m";
    public const string Dim = "\u001b[2m";
    public const string Red = "\u001b[31;1m";
    public const string Amber = "\u001b[33;1m";
    public const string Green = "\u001b[32;1m";
    public const string Teal = "\u001b[36;1m";
    public const string Grey = "\u001b[90m";

    public const int Width = 88;

    public static void Init(bool noColor)
    {
        if (_initialized) return;
        _initialized = true;
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch (IOException) { }

        if (noColor
            || Environment.GetEnvironmentVariable("NO_COLOR") is not null
            || string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase)
            || Console.IsOutputRedirected)
        {
            _color = false;
            return;
        }
        _color = !OperatingSystem.IsWindows() || TryEnableWindowsVirtualTerminal();
    }

    public static string C(string code, string text) => _color ? code + text + Reset : text;

    public static void Line(string s = "") => Console.WriteLine(s);

    /// <summary>
    /// A clickable link. The visible text is the URL itself, so it works twice over:
    /// terminals that understand OSC 8 hyperlinks (Windows Terminal, VS Code, iTerm2,
    /// most Linux terminals) get a real link, and the ones that don't still auto-detect
    /// a plain file:// or https:// URL. With colour off, it is just the URL.
    /// </summary>
    public static string Link(string url, string? text = null) =>
        _color ? $"\u001b]8;;{url}\u001b\\{text ?? url}\u001b]8;;\u001b\\" : text ?? url;

    /// <summary>file:///C:/path/to/file.html, escaped, for any local path.</summary>
    public static string FileUrl(string path) => new Uri(Path.GetFullPath(path)).AbsoluteUri;

    public static void Rule(char ch = '=') => Console.WriteLine(C(Grey, new string(ch, Width)));

    public static void Header(string title, string? subtitle = null)
    {
        Line();
        Rule();
        Line(C(Bold, title) + (subtitle is null ? "" : "   " + C(Grey, subtitle)));
        Rule();
    }

    public static void Section(string title)
    {
        Line();
        Line(C(Bold, title));
    }

    public static void Note(string text, int indent = 2)
    {
        foreach (var line in Wrap(text, Width - indent)) Line(new string(' ', indent) + C(Grey, line));
    }

    public static void Say(string text, int indent = 2)
    {
        foreach (var line in Wrap(text, Width - indent)) Line(new string(' ', indent) + line);
    }

    public static string Pass(string s = "PASS") => C(Green, "[" + s + "]");
    public static string Fail(string s = "FAIL") => C(Red, "[" + s + "]");
    public static string Warn(string s = "WARN") => C(Amber, "[" + s + "]");
    public static string Info(string s) => C(Teal, "[" + s + "]");

    public static string N(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    public static string M(decimal value) => value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    public static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > width)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    private static bool TryEnableWindowsVirtualTerminal()
    {
        try
        {
            const int STD_OUTPUT_HANDLE = -11;
            const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(handle, out var mode)) return false;
            if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0) return true;
            return SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}

public static class RepoRoot
{
    public const string Marker = "DotnetLegacyDemo.sln";

    public static string Find(string? start = null)
    {
        foreach (var origin in new[] { start, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(origin)) continue;
            var dir = new DirectoryInfo(Path.GetFullPath(origin));
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, Marker))) return dir.FullName;
                dir = dir.Parent;
            }
        }
        throw new DirectoryNotFoundException($"Could not find {Marker} above the current directory. Run from inside the repository, or pass --repo <path>.");
    }
}

/// <summary>
/// Name scrubbing for running the tools against a real codebase on stage.
///
/// `--redact names.txt` (or LEGACY_REDACT) loads lines of `Name=Replacement`. Every
/// console line, HTML fragment and page then has each Name replaced, case-insensitively,
/// wherever it starts a word or an identifier (so "ACME.Service" and "AcmeMetrics" are
/// caught, "SUPPORT" is not). Longest names are replaced first. The list lives next to
/// the code it describes, not in this repository.
/// </summary>
public static class Redactor
{
    private static List<(System.Text.RegularExpressions.Regex Pattern, string To)> _rules = new();

    public static bool Active => _rules.Count > 0;

    public static void Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) throw new FileNotFoundException($"No redaction list at {path}.");
        _rules = File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && l.Contains('='))
            .Select(l => (From: l[..l.IndexOf('=')].Trim(), To: l[(l.IndexOf('=') + 1)..].Trim()))
            .Where(r => r.From.Length > 0)
            .OrderByDescending(r => r.From.Length)
            .Select(r => (new System.Text.RegularExpressions.Regex(@"(?<![A-Za-z])" + System.Text.RegularExpressions.Regex.Escape(r.From),
                              System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant), r.To))
            .ToList();
        if (_rules.Count > 0) Console.SetOut(new RedactingWriter(Console.Out));
    }

    public static string Apply(string? s)
    {
        if (s is null || _rules.Count == 0) return s ?? "";
        foreach (var (pattern, to) in _rules) s = pattern.Replace(s, to);
        return s;
    }

    private sealed class RedactingWriter(TextWriter inner) : TextWriter
    {
        public override Encoding Encoding => inner.Encoding;
        public override void Write(char value) => inner.Write(value);
        public override void Write(string? value) => inner.Write(Apply(value));
        public override void WriteLine(string? value) => inner.WriteLine(Apply(value));
        public override void WriteLine() => inner.WriteLine();
        public override void Flush() => inner.Flush();
    }
}
