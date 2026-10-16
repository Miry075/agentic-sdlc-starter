using System.Text;
using System.Text.Json;
using AiReadiness.Scanner.Domain;

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
