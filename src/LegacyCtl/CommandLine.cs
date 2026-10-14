namespace LegacyCtl;

/// <summary>--flag value / --switch / positional. Forty lines instead of a NuGet package.</summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, string?> _named = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Positional { get; } = new();

    public CommandLine(IEnumerable<string> args)
    {
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var key = a[2..];
                string? value = null;
                var eq = key.IndexOf('=');
                if (eq >= 0) { value = key[(eq + 1)..]; key = key[..eq]; }
                else if (i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal)) value = list[++i];
                _named[key] = value;
            }
            else Positional.Add(a);
        }
    }

    public bool Has(string name) => _named.ContainsKey(name);
    public string? Get(string name) => _named.TryGetValue(name, out var v) ? v : null;
    public int GetInt(string name, int fallback) => int.TryParse(Get(name), out var v) ? v : fallback;
    public string? Arg(int index) => index < Positional.Count ? Positional[index] : null;
}

public sealed class Paths
{
    public required string Root { get; init; }
    public string Estate => Path.Combine(Root, "estate");
    public string Modern => Path.Combine(Root, "modern");
    public string Fixtures => Path.Combine(Root, "fixtures");
    public string Steps => Path.Combine(Root, "steps");
    public string Runtime => Path.Combine(Root, ".runtime");
    public string RatchetBaseline => Path.Combine(Root, "ratchet.baseline.json");

    public string Rel(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');
    public string Abs(string relative) => Path.GetFullPath(Path.Combine(Root, relative));
}
