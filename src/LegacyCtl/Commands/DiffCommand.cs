using LegacyDemo.Shared;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy diff a b: a unified diff with colour, so the demo does not depend on git being
/// on PATH, or on which git the presenter's machine happens to have.
/// </summary>
public static class DiffCommand
{
    public static int Run(Paths paths, CommandLine cli)
    {
        var a = cli.Arg(0) ?? throw new ArgumentException("usage: legacy diff <before> <after> [--context 2]");
        var b = cli.Arg(1) ?? throw new ArgumentException("usage: legacy diff <before> <after> [--context 2]");
        var (before, after, context) = (paths.Abs(a), paths.Abs(b), cli.GetInt("context", 2));
        var (added, removed) = Print(paths, before, after, context);
        if (cli.Get("html") is { } html)
        {
            var x = File.Exists(before) ? File.ReadAllLines(before) : Array.Empty<string>();
            var y = File.Exists(after) ? File.ReadAllLines(after) : Array.Empty<string>();
            Html.Emit(html, Views.Diff(paths.Rel(before), paths.Rel(after), Diff(x, y), context, added, removed));
        }
        return 0;
    }

    public static (int Added, int Removed) Print(Paths paths, string before, string after, int context)
    {
        var x = File.Exists(before) ? File.ReadAllLines(before) : Array.Empty<string>();
        var y = File.Exists(after) ? File.ReadAllLines(after) : Array.Empty<string>();
        var ops = Diff(x, y);

        Con.Line();
        Con.Line(Con.C(Con.Bold, "--- " + paths.Rel(before)));
        Con.Line(Con.C(Con.Bold, "+++ " + paths.Rel(after)));

        var changed = ops.Select((o, i) => (o, i)).Where(t => t.o.Kind != ' ').Select(t => t.i).ToList();
        var show = new bool[ops.Count];
        foreach (var i in changed)
            for (var k = Math.Max(0, i - context); k <= Math.Min(ops.Count - 1, i + context); k++) show[k] = true;

        int added = 0, removed = 0;
        var gap = false;
        for (var i = 0; i < ops.Count; i++)
        {
            if (!show[i]) { gap = true; continue; }
            if (gap) { Con.Line(Con.C(Con.Teal, $"@@ line {ops[i].LineB} @@")); gap = false; }
            var (kind, text, _) = ops[i];
            switch (kind)
            {
                case '+': added++; Con.Line(Con.C(Con.Green, "+" + text)); break;
                case '-': removed++; Con.Line(Con.C(Con.Red, "-" + text)); break;
                default: Con.Line(Con.C(Con.Grey, " " + text)); break;
            }
        }
        Con.Line();
        Con.Line(Con.C(Con.Grey, $"   {added} lines added, {removed} removed."));
        return (added, removed);
    }

    /// <summary>Classic LCS. Files here are hundreds of lines, not hundreds of thousands.</summary>
    public static List<(char Kind, string Text, int LineB)> Diff(string[] a, string[] b)
    {
        var n = a.Length; var m = b.Length;
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var ops = new List<(char, string, int)>();
        int p = 0, q = 0;
        while (p < n && q < m)
        {
            if (a[p] == b[q]) { ops.Add((' ', a[p], q + 1)); p++; q++; }
            else if (lcs[p + 1, q] >= lcs[p, q + 1]) { ops.Add(('-', a[p], q + 1)); p++; }
            else { ops.Add(('+', b[q], q + 1)); q++; }
        }
        while (p < n) ops.Add(('-', a[p++], q + 1));
        while (q < m) { ops.Add(('+', b[q], q + 1)); q++; }
        return ops;
    }
}
