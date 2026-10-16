using System.Text;
using System.Text.Json;
using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Remediation;

namespace AiReadiness.Scanner.Reporting;

public static class MarkdownReport
{
    private const int MaxActions = 5;

    public static string Render(Assessment assessment, string repoName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AI-readiness: {assessment.Score}/{assessment.MaxScore} ({Label(assessment.Band)})");
        sb.AppendLine();
        sb.AppendLine($"Repository: `{repoName}`");
        sb.AppendLine();
        sb.AppendLine("| Area | Score |");
        sb.AppendLine("|---|---|");
        foreach (var (area, points, max) in assessment.ByArea)
        {
            sb.AppendLine($"| {area} | {points}/{max} |");
        }

        sb.AppendLine();
        if (assessment.Gaps.Count == 0)
        {
            sb.AppendLine("Nothing to fix. Every criterion passed.");
            return sb.ToString();
        }

        sb.AppendLine("## Next actions, biggest gain first");
        sb.AppendLine();
        var number = 1;
        foreach (var gap in assessment.Gaps.Take(MaxActions))
        {
            sb.AppendLine($"{number++}. **+{gap.Weight}** {gap.Name}. {gap.Advice}");
        }

        if (assessment.Gaps.Count > MaxActions)
        {
            sb.AppendLine();
            sb.AppendLine($"{assessment.Gaps.Count - MaxActions} more in the JSON report (`--json`).");
        }

        return sb.ToString();
    }

    public static string RenderOrg(OrgAssessment org)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AI-readiness across {org.Ranked.Count} repositories: average {org.AverageScore}/100");
        sb.AppendLine();
        sb.AppendLine($"Strong {org.Count(ReadinessBand.Strong)} · Ready {org.Count(ReadinessBand.Ready)} · Partial {org.Count(ReadinessBand.Partial)} · Not ready {org.Count(ReadinessBand.NotReady)}");
        sb.AppendLine();
        sb.AppendLine("Lowest scores first.");
        sb.AppendLine();
        sb.AppendLine("| Repository | Score | Band | Biggest gap |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var r in org.Ranked)
        {
            var gap = r.Assessment.Gaps.FirstOrDefault();
            sb.AppendLine($"| {r.Name} | {r.Assessment.Score}/{r.Assessment.MaxScore} | {Label(r.Assessment.Band)} | {(gap is null ? "None" : $"+{gap.Weight} {gap.Name}")} |");
        }

        return sb.ToString();
    }

    public static string Label(ReadinessBand band) => band switch
    {
        ReadinessBand.Strong => "Strong",
        ReadinessBand.Ready => "Ready",
        ReadinessBand.Partial => "Partial",
        _ => "Not ready",
    };
}

public static class JsonReport
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>The fix plan for tools such as the /ai-readiness skill. <paramref name="appliedScore"/> is set after --apply.</summary>
    public static string RenderPlan(RemediationPlan plan, string repoName, int? appliedScore) =>
        JsonSerializer.Serialize(new
        {
            repository = repoName,
            applied = appliedScore is not null,
            score = appliedScore ?? plan.Before.Score,
            before = plan.Before.Score,
            afterAutomatic = plan.After.Score,
            potential = plan.PotentialScore,
            max = plan.Before.MaxScore,
            automatic = plan.Automatic.Select(f => new
            {
                title = f.Title,
                gain = f.Gain,
                files = f.Changes.Select(c => new { path = c.Path, executable = c.Executable }),
                notes = f.Notes,
            }),
            guided = plan.Guided.Select(g => new
            {
                title = g.Title,
                potentialGain = g.PotentialGain,
                criteria = g.Criteria,
                prompt = g.Prompt,
            }),
        }, Options);

    public static string RenderOrg(OrgAssessment org) =>
        JsonSerializer.Serialize(new
        {
            repositories = org.Ranked.Count,
            average = org.AverageScore,
            ranked = org.Ranked.Select(r => new
            {
                repository = r.Name,
                score = r.Assessment.Score,
                max = r.Assessment.MaxScore,
                band = MarkdownReport.Label(r.Assessment.Band),
                gaps = r.Assessment.Gaps.Select(g => new { area = g.Area, name = g.Name, weight = g.Weight }),
            }),
        }, Options);

    public static string Render(Assessment assessment, string repoName) =>
        JsonSerializer.Serialize(new
        {
            repository = repoName,
            score = assessment.Score,
            max = assessment.MaxScore,
            band = MarkdownReport.Label(assessment.Band),
            areas = assessment.ByArea.Select(a => new { area = a.Area, points = a.Points, max = a.Max }),
            criteria = assessment.Criteria.Select(c => new
            {
                area = c.Area,
                name = c.Name,
                weight = c.Weight,
                passed = c.Passed,
                advice = c.Passed ? null : c.Advice,
            }),
        }, Options);
}
