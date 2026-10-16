using System.Text.RegularExpressions;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed partial class SpecCheck : IReadinessCheck
{
    public string Area => "Specs per work item";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var specs = repo.Files("docs/specs")
            .Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                        && !System.IO.Path.GetFileName(p).Equals("TEMPLATE.md", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var numbered = specs.Count(p => NumberedSpec().IsMatch(System.IO.Path.GetFileName(p)));
        var withCriteria = specs.Count(p => repo.ReadText(p) is { } t && Acceptance().IsMatch(t));

        return
        [
            new(Area, "At least one committed spec in docs/specs", 5, specs.Count >= 1,
                "Commit a spec per work item in docs/specs/ so the build phase reads the agreed intent, not the backlog."),
            new(Area, "Specs are named by work item id (for example 1234.md)", 3, numbered >= 1,
                "Name each spec after its work item id so the gateway can find it from the work item."),
            new(Area, "Specs state acceptance criteria", 2, withCriteria >= 1,
                "Add an `Acceptance criteria` section to each spec. It is the part agents use most."),
        ];
    }

    [GeneratedRegex(@"^\d+.*\.md$")]
    private static partial Regex NumberedSpec();

    [GeneratedRegex(@"acceptance\s+criteria", RegexOptions.IgnoreCase)]
    private static partial Regex Acceptance();
}
