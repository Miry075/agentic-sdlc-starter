using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Remediation.Fixes;
using AiReadiness.Scanner.Remediation.Guided;

namespace AiReadiness.Scanner.Remediation;

public sealed class RemediationPlanner(
    ReadinessScanner scanner,
    IReadOnlyList<IAutomaticRemediation> automatic,
    IReadOnlyList<GuidedStep> guided,
    IAssets assets)
{
    public static IReadOnlyList<IAutomaticRemediation> DefaultFixes { get; } =
    [
        new ClaudeGuardFix(),
        new GitignoreFix(),
        new FirstAdrFix(),
        new ClaudeMdFix(),
        new CiChecksFix(),
    ];

    public RemediationPlan Plan(IRepoFiles repo, DateOnly today)
    {
        var before = scanner.Scan(repo);
        var context = new RemediationContext(repo, before, today, assets);

        var fixes = new List<AutomaticFix>();
        foreach (var proposed in automatic.Select(a => a.Propose(context)).OfType<ProposedFix>().Where(p => p.Changes.Count > 0))
        {
            var gain = scanner.Scan(new OverlayRepoFiles(repo, proposed.Changes)).Score - before.Score;
            fixes.Add(new AutomaticFix(proposed.Title, proposed.Changes, proposed.Notes, gain));
        }

        var after = scanner.Scan(new OverlayRepoFiles(repo, fixes.SelectMany(f => f.Changes)));

        var steps = new List<GuidedFix>();
        foreach (var step in guided)
        {
            var failing = after.Gaps.Where(step.Covers).ToList();
            if (failing.Count > 0)
            {
                steps.Add(new GuidedFix(step.Title, step.BuildPrompt(failing), failing.Sum(c => c.Weight)));
            }
        }

        return new RemediationPlan(before, after, fixes, steps.OrderByDescending(s => s.PotentialGain).ToList());
    }
}
