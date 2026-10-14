using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LegacyDemo.Shared;

namespace LegacyCtl.Estate;

public sealed class SourceFile
{
    public required string Project { get; set; }
    public required string RelPath { get; set; }     // relative to estate/
    public required string[] Lines { get; init; }
    public int CodeLines => Lines.Count(l => l.Trim().Length > 0 && !Rules.IsComment(l));
    public List<Hit> Hits { get; } = new();
    public List<(string Symbol, int Line)> Forks { get; } = new();
    public int Commits { get; set; }
    public int Authors { get; set; }

    public string? ClassName { get; set; }
    public int PublicMethods { get; set; }
    public int MaxCtorParams { get; set; }
    public int NamespacesReached { get; set; }

    public bool IsGodClass => CodeLines >= 500 || PublicMethods >= 25 || MaxCtorParams >= 10;
}

public sealed class Project
{
    public required string Name { get; set; }
    public required string RelPath { get; set; }
    public required bool Specimen { get; init; }
    public bool IsTest { get; set; }
    public required string Framework { get; init; }      // net10.0, net48, netstandard2.0 ...
    public required bool SdkStyle { get; init; }
    public List<string> BinaryReferences { get; } = new();   // HintPath -> checked-in DLLs
    public int WebFormsPages { get; set; }
    public bool IsNetFramework => Framework.StartsWith("net4", StringComparison.Ordinal);
    public List<string> References { get; } = new();
    public List<string> ReferencedBy { get; } = new();
    public HashSet<string> Dependents { get; } = new();     // transitive
    public List<SourceFile> Files { get; } = new();
    public int Lines => Files.Sum(f => f.CodeLines);
    public int Blockers => Files.Sum(f => f.Hits.Count);
    public int Commits => Files.Sum(f => f.Commits);
    public double BlockersPer100 => Lines == 0 ? 0 : Blockers * 100.0 / Lines;
}

public sealed class Procedure
{
    public required string Name { get; set; }
    public required string RelPath { get; set; }
    public required int CodeLines { get; init; }
    public required int Decisions { get; init; }
    public List<string> CalledFrom { get; } = new();
    public int Commits { get; set; }
    public bool HasBusinessLogic => Decisions >= 3;
}

/// <summary>Everything `legacy map` knows about the estate, read from disk as text.</summary>
public sealed class EstateModel
{
    public required string EstateRoot { get; init; }
    public List<Project> Projects { get; } = new();
    public List<Procedure> Procedures { get; } = new();
    public IEnumerable<SourceFile> Files => Projects.SelectMany(p => p.Files);

    /// <summary>Solution files in the estate: how many projects each lists, and how many of those exist.</summary>
    public List<(string Name, int Listed, int Missing)> Solutions { get; } = new();
    public int TestProjects { get; set; }
    public int TestMethods { get; set; }

    /// <summary>Where the change counts came from, for the report header (null = no history).</summary>
    public string? History { get; set; }

    /// <summary>Why there is no history, when git was asked and said no (e.g. "dubious ownership").</summary>
    public static string? GitError { get; private set; }

    // First segment of every project name ("Tallgrass", "Contoso", "Core"...): a `using` of one
    // of these is a dependency on the estate itself, not on the BCL or a package.
    private static HashSet<string> _roots = new(StringComparer.Ordinal) { "Tallgrass" };

    private static readonly Regex ClassDecl = new(@"\b(class|record|interface)\s+([A-Z]\w*)", RegexOptions.Compiled);
    private static readonly Regex PublicMethod = new(@"^\s*public\s+(static\s+|virtual\s+|override\s+|async\s+)*[\w<>\[\],\.\?]+\s+[A-Z]\w*\s*\(", RegexOptions.Compiled);
    private static readonly Regex UsingNs = new(@"^\s*using\s+(\w+)(\.[\w\.]+)?\s*;", RegexOptions.Compiled);
    private static readonly Regex TestAttr = new(@"\[(Fact|Theory|Test|TestMethod|TestCase)\b", RegexOptions.Compiled);
    private static readonly Regex SlnProject = new("\"([^\"]+\\.csproj)\"", RegexOptions.Compiled);
    // A decision point: a branch the procedure takes on data. CASE is counted by its WHENs.
    private static readonly Regex SqlDecision = new(@"\b(IF|WHEN|WHILE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ProcName = new(@"CREATE\s+PROCEDURE\s+(?:\[?dbo\]?\.)?\[?(\w+)\]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static EstateModel Load(string estateRoot)
    {
        if (!Directory.Exists(estateRoot)) throw new DirectoryNotFoundException($"No estate at {estateRoot}.");
        var model = new EstateModel { EstateRoot = estateRoot };

        var csprojs = Directory.EnumerateFiles(estateRoot, "*.csproj", SearchOption.AllDirectories).Where(NotBuildOutput).Order().ToList();
        _roots = new HashSet<string>(csprojs.Select(p => Path.GetFileNameWithoutExtension(p).Split('.')[0]), StringComparer.Ordinal);
        // A .cs file belongs to the nearest project above it, never to two.
        var projectDirs = csprojs.Select(p => Path.GetFullPath(Path.GetDirectoryName(p)!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? Owner(string file)
        {
            for (var d = Path.GetDirectoryName(Path.GetFullPath(file)); d is not null; d = Path.GetDirectoryName(d))
                if (projectDirs.Contains(d)) return d;
            return null;
        }

        foreach (var csproj in csprojs)
        {
            XDocument xml;
            try { xml = XDocument.Load(csproj); }
            catch (System.Xml.XmlException) { continue; }   // a broken project file is a finding for a human, not a crash
            var name = Path.GetFileNameWithoutExtension(csproj);
            var text = File.ReadAllText(csproj);
            var project = new Project
            {
                Name = name,
                RelPath = Rel(estateRoot, csproj),
                Specimen = text.Contains("SPECIMEN", StringComparison.Ordinal),
                Framework = FrameworkOf(xml, csproj),
                SdkStyle = xml.Root?.Attribute("Sdk") is not null,
            };
            foreach (var hint in xml.Descendants().Where(e => e.Name.LocalName == "HintPath"))
                project.BinaryReferences.Add(hint.Value.Trim().Replace('\\', '/'));
            foreach (var r in xml.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
            {
                var include = (string?)r.Attribute("Include");
                if (include is null) continue;
                project.References.Add(Path.GetFileNameWithoutExtension(include.Replace('\\', '/')));
            }
            var dir = Path.GetDirectoryName(csproj)!;
            var full = Path.GetFullPath(dir);
            foreach (var cs in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Where(NotBuildOutput).Order())
            {
                if (!string.Equals(Owner(cs), full, StringComparison.OrdinalIgnoreCase)) continue;
                project.Files.Add(ReadSource(estateRoot, name, cs));
            }
            var tests = project.Files.Sum(f => f.Lines.Count(l => TestAttr.IsMatch(l)));
            project.IsTest = tests > 0 || text.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase);
            if (project.IsTest) model.TestProjects++;
            model.TestMethods += tests;
            project.WebFormsPages = Directory.EnumerateFiles(dir, "*.aspx", SearchOption.AllDirectories).Count(NotBuildOutput);
            model.Projects.Add(project);
        }

        foreach (var p in model.Projects)
            foreach (var r in p.References)
                model.Find(r)?.ReferencedBy.Add(p.Name);
        foreach (var p in model.Projects) Walk(model, p.Name, p.Dependents);

        foreach (var sln in Directory.EnumerateFiles(estateRoot, "*.sln", SearchOption.AllDirectories).Where(NotBuildOutput).Order())
        {
            var listed = SlnProject.Matches(File.ReadAllText(sln)).Select(m => m.Groups[1].Value.Replace('\\', '/')).ToList();
            var dir = Path.GetDirectoryName(sln)!;
            model.Solutions.Add((Rel(estateRoot, sln), listed.Count, listed.Count(p => !File.Exists(Path.Combine(dir, p)))));
        }

        foreach (var sql in Directory.EnumerateFiles(estateRoot, "*.sql", SearchOption.AllDirectories).Where(NotBuildOutput).Order())
        {
            var lines = File.ReadAllLines(sql);
            var code = lines.Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("--", StringComparison.Ordinal)).ToList();
            var body = string.Join('\n', code.Select(l => { var i = l.IndexOf("--", StringComparison.Ordinal); return i >= 0 ? l[..i] : l; }));
            var m = ProcName.Match(body);
            var proc = new Procedure
            {
                Name = m.Success ? "dbo." + m.Groups[1].Value : Path.GetFileNameWithoutExtension(sql),
                RelPath = Rel(estateRoot, sql),
                CodeLines = code.Count,
                Decisions = SqlDecision.Matches(body).Count,
            };
            var bare = proc.Name.Replace("dbo.", "");
            foreach (var p in model.Projects)
                if (p.Files.Any(f => f.Lines.Any(l => l.Contains("\"" + bare + "\"", StringComparison.Ordinal))))
                    proc.CalledFrom.Add(p.Name);
            model.Procedures.Add(proc);
        }

        LoadChurn(model);
        if (Redactor.Active) model.Redact();
        return model;
    }

    /// <summary>
    /// With a redaction list loaded, rename everything the reports print at the source, so
    /// column widths are computed on the names the audience sees. (Output is scrubbed again
    /// on the way out; this pass is what keeps the tables lined up.)
    /// </summary>
    private void Redact()
    {
        static string R(string s) => Redactor.Apply(s);
        foreach (var p in Projects)
        {
            p.Name = R(p.Name);
            p.RelPath = R(p.RelPath);
            for (var i = 0; i < p.References.Count; i++) p.References[i] = R(p.References[i]);
            for (var i = 0; i < p.ReferencedBy.Count; i++) p.ReferencedBy[i] = R(p.ReferencedBy[i]);
            var deps = p.Dependents.Select(R).ToList();
            p.Dependents.Clear();
            foreach (var d in deps) p.Dependents.Add(d);
            for (var i = 0; i < p.BinaryReferences.Count; i++) p.BinaryReferences[i] = R(p.BinaryReferences[i]);
            foreach (var f in p.Files)
            {
                f.Project = R(f.Project);
                f.RelPath = R(f.RelPath);
                for (var i = 0; i < f.Hits.Count; i++) f.Hits[i] = f.Hits[i] with { File = R(f.Hits[i].File), Text = R(f.Hits[i].Text) };
            }
        }
        foreach (var proc in Procedures)
        {
            proc.Name = R(proc.Name);
            proc.RelPath = R(proc.RelPath);
            for (var i = 0; i < proc.CalledFrom.Count; i++) proc.CalledFrom[i] = R(proc.CalledFrom[i]);
        }
        for (var i = 0; i < Solutions.Count; i++) Solutions[i] = Solutions[i] with { Name = R(Solutions[i].Name) };
    }

    public Project? Find(string name) => Projects.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public SourceFile? FindFile(string relOrAbs)
    {
        var norm = relOrAbs.Replace('\\', '/');
        return Files.FirstOrDefault(f => norm.EndsWith(f.RelPath, StringComparison.OrdinalIgnoreCase));
    }

    private static void Walk(EstateModel model, string name, HashSet<string> seen)
    {
        var p = model.Find(name);
        if (p is null) return;
        foreach (var d in p.ReferencedBy)
            if (seen.Add(d)) Walk(model, d, seen);
    }

    public static SourceFile ReadSource(string estateRoot, string project, string path)
    {
        var lines = File.ReadAllLines(path);
        var file = new SourceFile { Project = project, RelPath = Rel(estateRoot, path), Lines = lines };
        var namespaces = new HashSet<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var fork = Rules.CompileFork.Match(line);
            if (fork.Success)
            {
                // DEBUG/TRACE and target-framework symbols are build plumbing, not a second program.
                var sym = fork.Groups[1].Value;
                if (!(sym is "DEBUG" or "TRACE" or "RELEASE" || sym.StartsWith("NET", StringComparison.Ordinal)))
                    file.Forks.Add((sym, i + 1));
                continue;
            }
            var u = UsingNs.Match(line);
            if (u.Success && u.Groups[2].Success && _roots.Contains(u.Groups[1].Value)) namespaces.Add(u.Groups[1].Value + u.Groups[2].Value);
            if (Rules.IsComment(line)) continue;
            var code = Rules.CodeOf(line);
            foreach (var rule in Rules.Blockers)
                foreach (Match _ in rule.Pattern.Matches(code))
                    file.Hits.Add(new Hit(rule, file.RelPath, i + 1, line.Trim()));
            if (file.ClassName is null)
            {
                var c = ClassDecl.Match(code);
                if (c.Success) file.ClassName = c.Groups[2].Value;
            }
        }
        if (file.ClassName is not null)
        {
            var ctor = new Regex(@"^\s*public\s+" + Regex.Escape(file.ClassName) + @"\s*\(([^)]*)\)");
            foreach (var line in lines)
            {
                var m = ctor.Match(line);
                if (m.Success)
                {
                    var p = m.Groups[1].Value.Trim();
                    file.MaxCtorParams = Math.Max(file.MaxCtorParams, p.Length == 0 ? 0 : p.Split(',').Length);
                }
                else if (PublicMethod.IsMatch(line)) file.PublicMethods++;
            }
        }
        file.NamespacesReached = namespaces.Count;
        return file;
    }

    private static void LoadChurn(EstateModel model)
    {
        var path = Path.Combine(model.EstateRoot, ".history", "churn-12mo.tsv");
        if (!File.Exists(path)) { GitChurn(model, Environment.GetEnvironmentVariable("LEGACY_GIT_LOG")); return; }
        model.History = ".history/churn-12mo.tsv";
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.Trim().Length == 0) continue;
            var p = line.Split('\t');
            if (p.Length < 3) continue;
            var commits = int.Parse(p[1]);
            var authors = int.Parse(p[2]);
            var file = model.Files.FirstOrDefault(f => f.RelPath.Equals(p[0], StringComparison.OrdinalIgnoreCase));
            if (file is not null) { file.Commits = commits; file.Authors = authors; continue; }
            var proc = model.Procedures.FirstOrDefault(x => x.RelPath.Equals(p[0], StringComparison.OrdinalIgnoreCase));
            if (proc is not null) proc.Commits = commits;
        }
    }

    /// <summary>
    /// No churn file: ask git. Change counts per file over the twelve months up to the
    /// LAST commit (an estate nobody has touched since 2020 still has a last year).
    /// Renames are followed (a file moved last spring keeps its history), so a big
    /// reorganization does not reset every hotspot to one commit.
    /// LEGACY_GIT_LOG points at a saved `git log --relative --name-status -M --format=@@%ae`
    /// instead, for machines without git or without the .git folder.
    /// </summary>
    private static void GitChurn(EstateModel model, string? savedLog)
    {
        string log, last;
        if (savedLog is not null && File.Exists(savedLog))
        {
            log = File.ReadAllText(savedLog);
            last = "the last commit";
        }
        else
        {
            last = Git(model.EstateRoot, "log", "-1", "--date=short", "--format=%ad").Trim();
            if (last.Length == 0) return;
            var since = DateOnly.Parse(last, System.Globalization.CultureInfo.InvariantCulture).AddDays(-365)
                .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            log = Git(model.EstateRoot, "log", "--since=" + since, "--relative", "--name-status", "-M", "--format=@@%ae");
        }
        var commits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var authors = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        // Newest commit first: when a rename shows up, everything older under the old path
        // belongs to the file's current path.
        var alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Current(string p) => alias.TryGetValue(p, out var c) ? c : p;
        var author = "";
        var total = 0;
        foreach (var raw in log.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("@@", StringComparison.Ordinal)) { author = line[2..]; total++; continue; }
            if (line.Trim().Length == 0) continue;
            var parts = line.Split('\t');
            string path;
            if (parts.Length == 1) path = parts[0].Trim();                       // --name-only
            else if (parts[0].StartsWith('R') || parts[0].StartsWith('C'))
            {
                path = Current(parts[2]);
                if (parts[0].StartsWith('R')) alias[parts[1]] = path;
                if (parts[0] is "R100") continue;                                // a pure move is not a change
            }
            else path = Current(parts[1]);
            if (!(path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))) continue;
            commits[path] = commits.GetValueOrDefault(path) + 1;
            if (!authors.TryGetValue(path, out var set)) authors[path] = set = new();
            set.Add(author);
        }
        if (total == 0) return;
        foreach (var f in model.Files)
            if (commits.TryGetValue(f.RelPath, out var c)) { f.Commits = c; f.Authors = authors[f.RelPath].Count; }
        foreach (var p in model.Procedures)
            if (commits.TryGetValue(p.RelPath, out var c)) p.Commits = c;
        model.History = $"git: {total.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} commits in the 12 months up to {last}";
    }

    private static string Git(string dir, params string[] args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            };
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(dir);
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = System.Diagnostics.Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(60_000);
            if (p.ExitCode != 0) GitError = stderr.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? $"git exited {p.ExitCode}";
            return p.ExitCode == 0 ? stdout.Result : "";
        }
        catch (System.ComponentModel.Win32Exception) { GitError = "git is not on PATH"; return ""; }   // no git on PATH: no hotspots, everything else still works
    }

    /// <summary>SDK-style TargetFramework, else old-style TargetFrameworkVersion (v4.8 -> net48), else the root props default.</summary>
    private static string FrameworkOf(XDocument xml, string csproj)
    {
        var tf = xml.Descendants().FirstOrDefault(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")?.Value.Split(';')[0].Trim();
        if (!string.IsNullOrEmpty(tf) && !tf.Contains('$')) return tf;
        var v = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetFrameworkVersion")?.Value.Trim();
        if (!string.IsNullOrEmpty(v)) return "net" + v.TrimStart('v').Replace(".", "");
        return xml.Root?.Attribute("Sdk") is not null ? "net10.0" : "unknown";
    }

    public static string Rel(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static bool NotBuildOutput(string path)
    {
        var p = path.Replace('\\', '/');
        return !p.Contains("/bin/", StringComparison.Ordinal) && !p.Contains("/obj/", StringComparison.Ordinal)
            && !p.Contains("/node_modules/", StringComparison.Ordinal) && !p.Contains("/.git/", StringComparison.Ordinal)
            && !p.Contains("/.vs/", StringComparison.Ordinal);
    }
}

public sealed class Signals
{
    public sealed class Entry
    {
        public int Incidents12mo { get; set; }
        public string Criticality { get; set; } = "";
        public string Roadmap { get; set; } = "";
        public string Usage { get; set; } = "";
        public string? ReplacedBy { get; set; }
    }

    public Dictionary<string, Entry> Projects { get; set; } = new();
    public Dictionary<string, Entry> Procedures { get; set; } = new();

    public static Signals Load(string estateRoot)
    {
        var path = Path.Combine(estateRoot, ".history", "signals.json");
        if (!File.Exists(path)) return new Signals();
        return JsonSerializer.Deserialize<Signals>(File.ReadAllText(path),
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })
               ?? new Signals();
    }

    public Entry For(string project) => Projects.TryGetValue(project, out var e) ? e : new Entry();
}
