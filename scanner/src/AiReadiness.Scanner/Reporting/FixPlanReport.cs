using System.Text;
using AiReadiness.Scanner.Remediation;

namespace AiReadiness.Scanner.Reporting;

public static class FixPlanReport
{
    public static string RenderPreview(RemediationPlan plan, string repoName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Fix plan for `{repoName}`: {plan.Before.Score}/{plan.Before.MaxScore}, up to {plan.PotentialScore}/{plan.Before.MaxScore} possible");
        sb.AppendLine();

        if (plan.Automatic.Count == 0)
        {
            sb.AppendLine("## Automatic fixes");
            sb.AppendLine();
            sb.AppendLine("Nothing the scanner can safely create by itself.");
        }
        else
        {
            sb.AppendLine($"## Automatic fixes: {plan.Before.Score} to {plan.After.Score} (+{plan.After.Score - plan.Before.Score})");
            sb.AppendLine();
            sb.AppendLine("Nothing has been written. Run again with `--fix --apply` to create these files. Existing files are never changed.");
            sb.AppendLine();
            var n = 1;
            foreach (var fix in plan.Automatic)
            {
                sb.AppendLine(fix.Gain > 0 ? $"{n++}. **+{fix.Gain}** {fix.Title}" : $"{n++}. **setup** {fix.Title}");
                foreach (var change in fix.Changes)
                {
                    sb.AppendLine($"   - create `{change.Path}`{(change.Executable ? " (executable)" : string.Empty)}");
                }

                foreach (var note in fix.Notes)
                {
                    sb.AppendLine($"   - {note}");
                }
            }
        }

        AppendGuided(sb, plan);
        return sb.ToString();
    }

    public static string RenderApplied(RemediationPlan plan, string repoName, int scoreAfter, int maxScore)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Applied to `{repoName}`: {plan.Before.Score}/{maxScore} to {scoreAfter}/{maxScore}");
        sb.AppendLine();
        sb.AppendLine("Created files (review them, then commit):");
        sb.AppendLine();
        foreach (var change in plan.AllChanges)
        {
            sb.AppendLine($"- `{change.Path}`");
        }

        foreach (var note in plan.Automatic.SelectMany(f => f.Notes).Distinct())
        {
            sb.AppendLine();
            sb.AppendLine($"Note: {note}");
        }

        AppendGuided(sb, plan);
        return sb.ToString();
    }

    private static void AppendGuided(StringBuilder sb, RemediationPlan plan)
    {
        if (plan.Guided.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("## Guided steps for Claude Code");
        sb.AppendLine();
        sb.AppendLine("These need someone who knows the system. Paste each prompt into Claude Code in this repository, review the result (it is a draft until you do), then run the scanner again.");
        var n = 1;
        foreach (var step in plan.Guided)
        {
            sb.AppendLine();
            sb.AppendLine($"### {n++}. {step.Title}: up to +{step.PotentialGain}");
            sb.AppendLine();
            sb.AppendLine("````text");
            sb.AppendLine(step.Prompt);
            sb.AppendLine("````");
        }
    }
}
