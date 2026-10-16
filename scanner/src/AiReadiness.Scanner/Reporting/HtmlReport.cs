using System.Net;
using System.Text;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Reporting;

public sealed record CallToAction(string Text, Uri Url);

/// <summary>A standalone page (no scripts, no external files) that can be e-mailed, hosted or printed to PDF.</summary>
public static class HtmlReport
{
    public static string Render(Assessment assessment, string repoName, DateOnly generatedOn, CallToAction? cta)
    {
        var sb = new StringBuilder();
        Head(sb, $"AI-readiness: {repoName} {assessment.Score}/{assessment.MaxScore}");
        sb.AppendLine("<header><p class=\"kicker\">AI-readiness report</p>");
        sb.AppendLine($"<h1>{E(repoName)}</h1><p class=\"meta\">{generatedOn:yyyy-MM-dd}</p></header>");
        sb.AppendLine($"<section class=\"score {BandClass(assessment.Band)}\"><div class=\"num\">{assessment.Score}<span>/{assessment.MaxScore}</span></div>");
        sb.AppendLine($"<div class=\"label\">{E(MarkdownReport.Label(assessment.Band))}</div></section>");

        sb.AppendLine("<section><h2>By area</h2>");
        foreach (var (area, points, max) in assessment.ByArea)
        {
            sb.AppendLine($"<div class=\"row\"><span>{E(area)}</span><span class=\"bar\"><i style=\"width:{Percent(points, max)}%\"></i></span><span class=\"pts\">{points}/{max}</span></div>");
        }

        sb.AppendLine("</section>");

        sb.AppendLine("<section><h2>Next actions, biggest gain first</h2>");
        if (assessment.Gaps.Count == 0)
        {
            sb.AppendLine("<p>Nothing to fix. Every criterion passed.</p>");
        }
        else
        {
            sb.AppendLine("<ol>");
            foreach (var gap in assessment.Gaps.Take(5))
            {
                sb.AppendLine($"<li><strong>+{gap.Weight}</strong> {E(gap.Name)}<br><span class=\"muted\">{E(gap.Advice)}</span></li>");
            }

            sb.AppendLine("</ol>");
        }

        sb.AppendLine("</section>");

        sb.AppendLine("<section><h2>All checks</h2>");
        foreach (var group in assessment.Criteria.GroupBy(c => c.Area))
        {
            sb.AppendLine($"<h3>{E(group.Key)}</h3><ul class=\"checks\">");
            foreach (var c in group)
            {
                sb.AppendLine($"<li class=\"{(c.Passed ? "pass" : "fail")}\"><span class=\"mark\">{(c.Passed ? "Pass" : "Fail")}</span> {E(c.Name)} <span class=\"muted\">({c.Weight})</span></li>");
            }

            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</section>");
        Foot(sb, cta);
        return sb.ToString();
    }

    public static string RenderOrg(OrgAssessment org, DateOnly generatedOn, CallToAction? cta)
    {
        var sb = new StringBuilder();
        Head(sb, $"AI-readiness: {org.Ranked.Count} repositories, average {org.AverageScore}/100");
        sb.AppendLine("<header><p class=\"kicker\">AI-readiness report</p>");
        sb.AppendLine($"<h1>{org.Ranked.Count} repositories</h1><p class=\"meta\">{generatedOn:yyyy-MM-dd}</p></header>");
        sb.AppendLine($"<section class=\"score\"><div class=\"num\">{org.AverageScore}<span>/100</span></div><div class=\"label\">Average</div></section>");
        sb.AppendLine($"<p class=\"muted\">Strong {org.Count(ReadinessBand.Strong)} · Ready {org.Count(ReadinessBand.Ready)} · Partial {org.Count(ReadinessBand.Partial)} · Not ready {org.Count(ReadinessBand.NotReady)}</p>");
        sb.AppendLine("<section><h2>Lowest scores first</h2><table><thead><tr><th>Repository</th><th>Score</th><th>Band</th><th>Biggest gap</th></tr></thead><tbody>");
        foreach (var r in org.Ranked)
        {
            var a = r.Assessment;
            var gap = a.Gaps.FirstOrDefault();
            sb.AppendLine($"<tr><td>{E(r.Name)}</td><td><span class=\"bar\"><i style=\"width:{Percent(a.Score, a.MaxScore)}%\"></i></span> {a.Score}</td><td class=\"{BandClass(a.Band)}\">{E(MarkdownReport.Label(a.Band))}</td><td>{(gap is null ? "None" : $"<strong>+{gap.Weight}</strong> {E(gap.Name)}")}</td></tr>");
        }

        sb.AppendLine("</tbody></table></section>");
        Foot(sb, cta);
        return sb.ToString();
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static int Percent(int points, int max) => max == 0 ? 0 : points * 100 / max;

    private static string BandClass(ReadinessBand band) => band switch
    {
        ReadinessBand.Strong => "strong",
        ReadinessBand.Ready => "ready",
        ReadinessBand.Partial => "partial",
        _ => "notready",
    };

    private static void Head(StringBuilder sb, string title)
    {
        sb.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.AppendLine($"<title>{E(title)}</title><style>{Css}</style></head><body><main>");
    }

    private static void Foot(StringBuilder sb, CallToAction? cta)
    {
        if (cta is not null)
        {
            sb.AppendLine($"<footer><a class=\"cta\" href=\"{E(cta.Url.AbsoluteUri)}\">{E(cta.Text)}</a></footer>");
        }

        sb.AppendLine("</main></body></html>");
    }

    private const string Css = """
        :root{--bg:#fff;--fg:#1f1f1c;--muted:#6b6a64;--line:#e3e1d8;--card:#f6f5f0;--bar:#534ab7;--strong:#0f6e56;--ready:#3b6d11;--partial:#854f0b;--notready:#a32d2d}
        @media (prefers-color-scheme:dark){:root{--bg:#161614;--fg:#ecebe4;--muted:#a3a198;--line:#2f2e2a;--card:#201f1c;--bar:#9b94ee;--strong:#5dcaa5;--ready:#97c459;--partial:#ef9f27;--notready:#f09595}}
        *{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:16px/1.6 system-ui,-apple-system,Segoe UI,sans-serif}
        main{max-width:760px;margin:0 auto;padding:32px 16px 64px}h1{font-size:28px;margin:4px 0;font-weight:500}h2{font-size:18px;font-weight:500;margin:32px 0 12px}h3{font-size:15px;font-weight:500;margin:20px 0 6px}
        .kicker{margin:0;color:var(--muted);font-size:13px;text-transform:uppercase;letter-spacing:.06em}.meta,.muted{color:var(--muted);font-size:14px}
        .score{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:20px 24px;margin:20px 0}.num{font-size:56px;line-height:1.1;font-weight:500}.num span{font-size:20px;color:var(--muted)}.label{font-size:18px;font-weight:500}
        .strong .label,td.strong{color:var(--strong)}.ready .label,td.ready{color:var(--ready)}.partial .label,td.partial{color:var(--partial)}.notready .label,td.notready{color:var(--notready)}
        .row{display:grid;grid-template-columns:minmax(0,1fr) 120px 48px;gap:12px;align-items:center;padding:6px 0;border-bottom:1px solid var(--line)}.pts{text-align:right}
        .bar{display:inline-block;width:120px;height:8px;background:var(--line);border-radius:4px;overflow:hidden;vertical-align:middle}.bar i{display:block;height:100%;background:var(--bar)}
        ol{padding-left:22px}li{margin:8px 0}.checks{list-style:none;padding:0;margin:0}.checks li{margin:4px 0;font-size:15px}.mark{display:inline-block;min-width:38px;font-weight:500;font-size:13px}.pass .mark{color:var(--strong)}.fail .mark{color:var(--notready)}
        table{width:100%;border-collapse:collapse;font-size:15px}th,td{text-align:left;padding:8px 6px;border-bottom:1px solid var(--line);vertical-align:top}th{color:var(--muted);font-weight:500}
        footer{margin-top:40px}.cta{display:inline-block;background:var(--bar);color:var(--bg);padding:10px 18px;border-radius:8px;text-decoration:none;font-weight:500}
        @media (max-width:520px){.row{grid-template-columns:minmax(0,1fr) 48px}.row .bar{display:none}}
        @media print{body{background:#fff;color:#000}.cta{border:1px solid #000;background:none;color:#000}}
        """;
}
