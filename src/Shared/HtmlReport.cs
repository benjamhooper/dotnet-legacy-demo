using System.Globalization;
using System.Net;
using System.Text;

namespace LegacyDemo.Shared;

/// <summary>
/// HTML versions of every report, for the projector.
///
/// Each command that is given `--html &lt;file&gt;` writes one self-contained fragment
/// (a &lt;section&gt;) to that file; `legacy page` stitches a demo's fragments, in order,
/// into a single page with the styles below. No CDN, no JavaScript framework, no web
/// server: the pages open from disk with file://, offline, like everything else here.
///
/// Colours are the Clear Measure deck palette. Status is always carried by an icon and
/// a word as well as colour (the amber fails 3:1 contrast on white on its own).
/// </summary>
public static class Html
{
    public static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    public static string N(int v) => v.ToString("N0", CultureInfo.InvariantCulture);
    public static string M(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Writes a fragment if the command was asked for one; otherwise does nothing.</summary>
    public static void Emit(string? path, string fragment)
    {
        if (string.IsNullOrEmpty(path)) return;
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, Redactor.Apply(fragment), new UTF8Encoding(false));
    }

    public enum Tone { Good, Warn, Bad, Info, Muted }

    private static string Cls(Tone t) => t switch
    {
        Tone.Good => "good", Tone.Warn => "warn", Tone.Bad => "bad", Tone.Info => "info", _ => "muted",
    };

    private static string Icon(Tone t) => t switch
    {
        Tone.Good => "&#10003;", Tone.Warn => "&#9650;", Tone.Bad => "&#10005;", Tone.Info => "&#9679;", _ => "&#9675;",
    };

    public static string Badge(string text, Tone tone) =>
        $"<span class=\"badge {Cls(tone)}\"><i>{Icon(tone)}</i>{E(text)}</span>";

    public static string Chip(string text, string? title = null) =>
        $"<span class=\"chip\"{(title is null ? "" : $" title=\"{E(title)}\"")}>{E(text)}</span>";

    public static string Section(string eyebrow, string title, string body, string? sub = null) =>
        $"<section class=\"card\"><div class=\"eyebrow\">{E(eyebrow)}</div><h2>{E(title)}</h2>" +
        (sub is null ? "" : $"<p class=\"sub\">{E(sub)}</p>") + body + "</section>\n";

    public static string H3(string text) => $"<h3>{E(text)}</h3>";

    public static string P(string text, string cls = "") => $"<p class=\"{cls}\">{E(text)}</p>";

    public static string Tiles(params (string Label, string Value, Tone Tone)[] tiles)
    {
        var sb = new StringBuilder("<div class=\"tiles\">");
        foreach (var (label, value, tone) in tiles)
            sb.Append($"<div class=\"tile {Cls(tone)}\"><div class=\"v\">{E(value)}</div><div class=\"l\">{E(label)}</div></div>");
        return sb.Append("</div>").ToString();
    }

    /// <summary>A horizontal bar on a shared scale; the value label sits outside the bar end.</summary>
    public static string Bar(double value, double max, Tone tone, string label, string? title = null)
    {
        var pct = max <= 0 ? 0 : Math.Max(0, Math.Min(100, value / max * 100));
        return $"<div class=\"bar\" title=\"{E(title ?? label)}\"><span class=\"fill {Cls(tone)}\" style=\"width:{pct.ToString("0.#", CultureInfo.InvariantCulture)}%\"></span><b>{E(label)}</b></div>";
    }

    /// <summary>One stacked bar of parts that add up to a whole, with a legend underneath.</summary>
    public static string Stack(params (string Label, int Value, Tone Tone)[] parts)
    {
        var total = Math.Max(1, parts.Sum(p => p.Value));
        var sb = new StringBuilder("<div class=\"stack\">");
        foreach (var (label, value, tone) in parts.Where(p => p.Value > 0))
            sb.Append($"<span class=\"seg {Cls(tone)}\" style=\"flex:{value}\" title=\"{E(label)}: {N(value)}\"></span>");
        sb.Append("</div><div class=\"legend\">");
        foreach (var (label, value, tone) in parts)
            sb.Append($"<span><i class=\"sw {Cls(tone)}\"></i>{E(label)} <b>{N(value)}</b> <em>({(value * 100.0 / total).ToString("0.#", CultureInfo.InvariantCulture)}%)</em></span>");
        return sb.Append("</div>").ToString();
    }

    public static string Callout(string text, Tone tone) =>
        $"<div class=\"callout {Cls(tone)}\"><i>{Icon(tone)}</i><div>{E(text)}</div></div>";

    public static string Verdict(string word, string text, Tone tone) =>
        $"<div class=\"verdict {Cls(tone)}\"><div class=\"word\"><i>{Icon(tone)}</i>{E(word)}</div><div class=\"why\">{E(text)}</div></div>";

    /// <summary>Code with optional line numbers and highlighted lines.</summary>
    public static string Code(IEnumerable<(int? No, string Text, string Kind)> lines, string? caption = null)
    {
        var list = lines.ToList();
        var numbered = list.Any(l => l.No is not null);
        var sb = new StringBuilder($"<figure class=\"code{(numbered ? " numbered" : "")}\">");
        if (caption is not null) sb.Append($"<figcaption>{E(caption)}</figcaption>");
        sb.Append("<pre>");
        foreach (var (no, text, kind) in list)
        {
            // Every line in a numbered block gets the gutter, so removed lines (no number) stay aligned.
            var num = numbered ? $"<span class=\"ln\">{no}</span>" : "";
            sb.Append($"<span class=\"line {kind}\">{num}{E(text.Length == 0 ? " " : text)}</span>");
        }
        return sb.Append("</pre></figure>").ToString();
    }

    public static string Table(string[] headers, IEnumerable<string[]> rows, string cls = "")
    {
        var sb = new StringBuilder($"<div class=\"tablewrap\"><table class=\"{cls}\"><thead><tr>");
        foreach (var h in headers) sb.Append($"<th>{E(h)}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var r in rows) sb.Append("<tr>").Append(string.Concat(r.Select(c => $"<td>{c}</td>"))).Append("</tr>");
        return sb.Append("</tbody></table></div>").ToString();
    }

    public static string Grid(IEnumerable<string> cards, int columns = 2) =>
        $"<div class=\"grid g{columns}\">{string.Concat(cards)}</div>";

    public static string MiniCard(string badgeHtml, string title, string body, string? meta = null) =>
        $"<div class=\"mini\">{badgeHtml}<h4>{E(title)}</h4><p>{E(body)}</p>{(meta is null ? "" : $"<div class=\"meta\">{E(meta)}</div>")}</div>";

    public static string Flow(params string[] steps) =>
        "<div class=\"flow\">" + string.Join("<span class=\"arrow\">&#8594;</span>", steps.Select(s => $"<span class=\"node\">{E(s)}</span>")) + "</div>";

    // ---------------------------------------------------------------------------------
    // the page
    // ---------------------------------------------------------------------------------

    public static string Page(string label, string title, string subtitle, string body, IEnumerable<(string File, string Label)> nav, string? current = null)
    {
        var navHtml = NavLinks(nav, current);
        return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{E(label)}} · {{E(title)}}</title>
<style>{{Css}}</style>
</head>
<body>
<header class="band">
  <div class="wrap">
    <div class="label">{{E(label)}}</div>
    <h1>{{E(title)}}</h1>
    <p>{{E(subtitle)}}</p>
    <nav>{{navHtml}}</nav>
  </div>
</header>
<main class="wrap">
{{body}}
</main>
<footer class="wrap">Generated {{DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}} by <code>legacy</code> · Real-World .NET in the Enterprise</footer>
</body>
</html>
""";
    }

    public static string NavLinks(IEnumerable<(string File, string Label)> nav, string? current = null) =>
        string.Concat(nav.Select(n => $"<a href=\"{E(n.File)}\"{(n.File == current ? " class=\"on\"" : "")}>{E(n.Label)}</a>"));

    public static string Beat(int n, string text) =>
        $"<div class=\"beat\"><span class=\"n\">{n}</span><span>{E(text)}</span></div>\n";

    private const string Css = """
:root{
  --navy:#0A1929;--navy2:#102A3F;--ink:#0A1929;--ink2:#3B4A57;--muted:#72828F;--faint:#C6D0D9;
  --surface:#F5F7F9;--card:#FFFFFF;--line:#E3E9EE;--blue:#0092DB;--blue-d:#0064B1;
  --good:#3F8C60;--warn:#D9902B;--bad:#BF3F35;--info:#0092DB;--mute:#8A97A3;
  --good-t:#E7F3EC;--warn-t:#FBF1E1;--bad-t:#F8E5E3;--info-t:#E2F3FC;--mute-t:#EEF1F4;
  --head:'Poppins','Segoe UI Semibold','Segoe UI',system-ui,sans-serif;
  --body:'Lato','Segoe UI',system-ui,-apple-system,sans-serif;
  --mono:'Cascadia Mono','Cascadia Code',Consolas,'Courier New',monospace;
}
*{box-sizing:border-box}
html{font-size:18px}
body{margin:0;background:var(--surface);color:var(--ink);font-family:var(--body);line-height:1.45}
.wrap{max-width:1240px;margin:0 auto;padding:0 28px}
.band{background:linear-gradient(90deg,#0064B1 0,#0092DB 100%) top/100% 8px no-repeat,var(--navy);color:#fff;padding:34px 0 22px}
.band .label{font-family:var(--mono);font-weight:700;color:var(--warn);letter-spacing:.08em;font-size:.8rem;text-transform:uppercase}
.band h1{font-family:var(--head);font-size:2.3rem;margin:.15em 0 .1em;line-height:1.15}
.band p{margin:0;color:var(--faint);font-size:1.05rem}
.band nav{margin-top:18px;display:flex;flex-wrap:wrap;gap:8px}
.band nav a{font-family:var(--mono);font-size:.72rem;color:#fff;text-decoration:none;border:1px solid #2C3A46;border-radius:999px;padding:4px 11px;background:var(--navy2)}
.band nav a:hover{border-color:var(--blue)}
.band nav a.on{background:var(--blue-d);border-color:var(--blue)}
main{padding:22px 28px 40px}
footer{color:var(--muted);font-size:.72rem;padding-bottom:28px}
.beat{display:flex;gap:14px;align-items:flex-start;margin:26px 0 12px;font-family:var(--head);font-size:1.25rem;font-weight:600;color:var(--ink)}
.beat .n{flex:none;width:34px;height:34px;border-radius:50%;background:var(--navy);color:#fff;display:grid;place-items:center;font-size:.9rem;margin-top:2px}
.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:22px 26px;margin:0 0 18px;box-shadow:0 1px 2px rgba(10,25,41,.04)}
.eyebrow{font-family:var(--mono);font-size:.72rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:var(--muted)}
h2{font-family:var(--head);font-size:1.45rem;margin:.1em 0 .35em;line-height:1.2}
h3{font-family:var(--head);font-size:1.05rem;margin:1.3em 0 .5em;color:var(--ink)}
h4{font-family:var(--head);font-size:1rem;margin:.45em 0 .25em}
.sub{color:var(--ink2);margin:.1em 0 1em}
p{margin:.4em 0}
.note{color:var(--ink2)}
code,.mono{font-family:var(--mono);font-size:.88em}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:12px;margin:12px 0 6px}
.tile{border:1px solid var(--line);border-radius:10px;padding:14px 16px;background:var(--surface)}
.tile .v{font-family:var(--head);font-weight:700;font-size:2rem;line-height:1.1}
.tile .l{color:var(--ink2);font-size:.85rem}
.tile.good{border-top:4px solid var(--good)}.tile.warn{border-top:4px solid var(--warn)}.tile.bad{border-top:4px solid var(--bad)}.tile.info{border-top:4px solid var(--info)}
.badge{display:inline-flex;align-items:center;gap:6px;font-family:var(--mono);font-size:.72rem;font-weight:700;letter-spacing:.03em;padding:3px 9px;border-radius:999px;white-space:nowrap;color:var(--ink)}
.badge i{font-style:normal;font-size:.8em}
.badge.good{background:var(--good-t)}.badge.good i{color:var(--good)}
.badge.warn{background:var(--warn-t)}.badge.warn i{color:#B06F12}
.badge.bad{background:var(--bad-t)}.badge.bad i{color:var(--bad)}
.badge.info{background:var(--info-t)}.badge.info i{color:var(--blue-d)}
.badge.muted{background:var(--mute-t)}.badge.muted i{color:var(--mute)}
.chip{display:inline-block;font-family:var(--mono);font-size:.72rem;padding:2px 8px;border-radius:6px;background:var(--mute-t);color:var(--ink2);margin:2px 4px 2px 0}
.bar{position:relative;display:flex;align-items:center;gap:10px;min-width:160px}
.bar .fill{display:block;height:14px;border-radius:0 4px 4px 0;min-width:2px}
.bar b{font-family:var(--mono);font-size:.78rem;font-weight:700;color:var(--ink);white-space:nowrap}
.fill.good,.seg.good,.sw.good{background:var(--good)}.fill.warn,.seg.warn,.sw.warn{background:var(--warn)}
.fill.bad,.seg.bad,.sw.bad{background:var(--bad)}.fill.info,.seg.info,.sw.info{background:var(--blue)}
.fill.muted,.seg.muted,.sw.muted{background:var(--faint)}
.stack{display:flex;gap:2px;height:30px;margin:14px 0 8px;border-radius:6px;overflow:hidden}
.stack .seg{display:block}
.legend{display:flex;flex-wrap:wrap;gap:18px;font-size:.85rem;color:var(--ink2)}
.legend .sw{display:inline-block;width:12px;height:12px;border-radius:3px;margin-right:6px;vertical-align:-1px}
.legend em{color:var(--muted);font-style:normal}
.tablewrap{overflow-x:auto}
table{border-collapse:collapse;width:100%;font-size:.88rem}
th{font-family:var(--mono);font-size:.68rem;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--muted);text-align:left;padding:8px 10px;border-bottom:1px solid var(--line)}
td{padding:9px 10px;border-bottom:1px solid var(--line);vertical-align:middle}
td.num,th.num{text-align:right;font-family:var(--mono)}
tr:hover td{background:#FAFBFC}
.callout{display:flex;gap:12px;align-items:flex-start;border-radius:10px;padding:14px 16px;margin:14px 0 4px;font-size:1rem}
.callout i{font-style:normal;font-weight:700;margin-top:1px}
.callout.good{background:var(--good-t)}.callout.good i{color:var(--good)}
.callout.warn{background:var(--warn-t)}.callout.warn i{color:#B06F12}
.callout.bad{background:var(--bad-t)}.callout.bad i{color:var(--bad)}
.callout.info{background:var(--info-t)}.callout.info i{color:var(--blue-d)}
.callout.muted{background:var(--mute-t)}
.verdict{display:flex;gap:22px;align-items:center;border-radius:12px;padding:18px 22px;margin:16px 0 4px;color:#fff}
.verdict .word{font-family:var(--head);font-weight:700;font-size:2rem;display:flex;gap:12px;align-items:center;white-space:nowrap}
.verdict .why{font-size:1.02rem;opacity:.95}
.verdict.good{background:var(--good)}.verdict.bad{background:var(--bad)}.verdict.warn{background:#B06F12}.verdict.info{background:var(--blue-d)}.verdict.muted{background:var(--ink2)}
.code{margin:12px 0;border-radius:10px;overflow:hidden;background:var(--navy);border:1px solid var(--navy2)}
.code figcaption{font-family:var(--mono);font-size:.7rem;color:var(--faint);padding:8px 14px;background:var(--navy2)}
.code pre{margin:0;padding:12px 0;overflow-x:auto;font-family:var(--mono);font-size:.82rem;line-height:1.55;color:#D5DEE5}
.code .line{display:block;padding:0 16px;white-space:pre-wrap;overflow-wrap:anywhere}
.code.numbered .line{padding-left:calc(16px + 3.2em);text-indent:-3.2em}
.code .ln{display:inline-block;width:3.2em;text-indent:0;color:#5E6E7C;user-select:none}
.code .add{background:rgba(63,140,96,.28);color:#C9F0D6}
.code .del{background:rgba(191,63,53,.30);color:#F6CFCB}
.code .hot{background:rgba(217,144,43,.22);color:#FFE2B5}
.code .dim{color:#7C8B98}
.code .hunk{color:#6CC4F0}
.grid{display:grid;gap:14px;margin:12px 0}
.grid.g2{grid-template-columns:repeat(auto-fit,minmax(340px,1fr))}
.grid.g3{grid-template-columns:repeat(auto-fit,minmax(260px,1fr))}
.grid.g4{grid-template-columns:repeat(auto-fit,minmax(220px,1fr))}
.mini{border:1px solid var(--line);border-radius:10px;padding:14px 16px;background:var(--surface)}
.mini p{color:var(--ink2);font-size:.9rem;margin:.2em 0}
.mini .meta{font-family:var(--mono);font-size:.72rem;color:var(--muted);margin-top:6px;overflow-wrap:anywhere}
.result{margin:4px 0 14px}.result .badge{font-size:.85rem;padding:5px 12px}
a.mini.link{display:block;color:inherit;text-decoration:none;transition:border-color .15s}
a.mini.link:hover{border-color:var(--blue)}
.mini.off{opacity:.6;border-style:dashed}
.flow{display:flex;flex-wrap:wrap;align-items:center;gap:10px;margin:14px 0}
.flow .node{font-family:var(--mono);font-size:.82rem;font-weight:700;background:var(--surface);border:1px solid var(--line);border-radius:8px;padding:8px 12px}
.flow .node:last-child{background:var(--bad-t);border-color:#E9B7B2}
.flow .arrow{color:var(--muted);font-weight:700}
.graph{width:100%;height:auto;display:block;margin:6px 0}
.graph text{font-family:var(--body);font-size:12px;fill:var(--ink)}
.graph .edge{fill:none;stroke:#B9C5CF;stroke-width:1.2}
.graph .edge.hot{stroke:var(--bad);stroke-opacity:.35}
.graph .nodebox{fill:#fff;stroke:#C6D0D9;stroke-width:1.2}
.graph .nodebox.hub{fill:var(--bad-t);stroke:var(--bad)}
.graph .nodebox.fw{fill:var(--warn-t);stroke:var(--warn)}
.graph .nodebox.start{fill:var(--good-t);stroke:var(--good)}
.graph .lvl{font-family:var(--mono);font-size:10px;fill:var(--muted)}
.meter{display:flex;gap:2px;margin:12px 0}
.meter span{flex:1;height:22px;border-radius:2px}
.big{font-family:var(--head);font-weight:700;font-size:3rem;line-height:1}
.split{display:grid;grid-template-columns:auto 1fr;gap:26px;align-items:center}
/* Projector mode: numbers, badges and verdicts only. The console keeps the full wording. */
.sub,p.note,.beat,.mini p,.mini .meta,.verdict .why{display:none}
.mini h4{margin:.5em 0 0}
@media (max-width:700px){html{font-size:16px}.split{grid-template-columns:1fr}.verdict{flex-direction:column;align-items:flex-start}}
@media print{.band nav{display:none}.card{break-inside:avoid}}
""";
}
