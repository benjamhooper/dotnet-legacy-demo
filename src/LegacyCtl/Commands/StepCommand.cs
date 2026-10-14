using LegacyDemo.Shared;

namespace LegacyCtl.Commands;

/// <summary>
/// legacy step NAME / legacy reset: move the repository between demo states.
///
/// Every live change in the talk is a pre-written step in steps/, applied by copying
/// files. Nothing is typed on stage. A step folder mirrors the repository layout;
/// an optional _delete.txt lists repository-relative files the step removes.
/// </summary>
public static class StepCommand
{
    public const string Pristine = "00-pristine";

    public static int Run(Paths paths, CommandLine cli)
    {
        var name = cli.Arg(0) ?? throw new ArgumentException("usage: legacy step <name>   (see steps/)");
        var changed = Apply(paths, name);
        Con.Line();
        Con.Line($"   {Con.Info("STEP")} {name}");
        foreach (var c in changed) Con.Line(Con.C(Con.Grey, "      " + c));
        var note = Path.Combine(paths.Steps, name, "_step.txt");
        string? text = File.Exists(note) ? File.ReadAllText(note).Trim() : null;
        if (text is not null) Con.Note(text, 6);
        Con.Line();
        Html.Emit(cli.Get("html"), Views.Step(name, changed, text));
        return 0;
    }

    public static int Reset(Paths paths, CommandLine cli)
    {
        var changed = Apply(paths, Pristine);
        if (Directory.Exists(paths.Runtime)) Directory.Delete(paths.Runtime, recursive: true);
        Con.Line();
        Con.Line($"   {Con.Pass("RESET")} {(changed.Count == 0 ? "already in" : $"{changed.Count} file{(changed.Count == 1 ? "" : "s")} restored to")} the day-one state.");
        Con.Line();
        return 0;
    }

    public static List<string> Apply(Paths paths, string name)
    {
        var dir = Path.Combine(paths.Steps, name);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException($"No step '{name}' under steps/.");
        var changed = new List<string>();
        foreach (var src in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order())
        {
            var rel = Path.GetRelativePath(dir, src);
            if (rel is "_delete.txt" or "_step.txt") continue;
            var dest = Path.Combine(paths.Root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var existed = File.Exists(dest);
            if (existed && File.ReadAllBytes(dest).AsSpan().SequenceEqual(File.ReadAllBytes(src))) continue;
            File.Copy(src, dest, overwrite: true);
            // File.Copy keeps the source's timestamp. MSBuild's incremental build then sees a
            // file "older" than the last build output and skips it, and the demo silently
            // runs yesterday's code. Touch it.
            File.SetLastWriteTimeUtc(dest, DateTime.UtcNow);
            changed.Add((existed ? "M " : "A ") + rel.Replace('\\', '/'));
        }
        var deletes = Path.Combine(dir, "_delete.txt");
        if (File.Exists(deletes))
        {
            foreach (var line in File.ReadAllLines(deletes).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')))
            {
                var target = Path.Combine(paths.Root, line);
                if (!File.Exists(target)) continue;
                File.Delete(target);
                changed.Add("D " + line);
            }
        }
        return changed;
    }
}
