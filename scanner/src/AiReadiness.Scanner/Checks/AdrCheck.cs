using System.Text.RegularExpressions;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed partial class AdrCheck : IReadinessCheck
{
    public string Area => "Architecture decisions (ADRs)";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var adrs = RealAdrs(repo);
        var accepted = adrs.Count(p => repo.ReadText(p) is { } t && AcceptedStatus().IsMatch(t));

        return
        [
            new(Area, "At least one ADR in docs/adr", 4, adrs.Count >= 1,
                "Record your first decision in docs/adr/0001-<title>.md. Start with the decision to record decisions."),
            new(Area, "At least three ADRs", 3, adrs.Count >= 3,
                "Write ADRs for the decisions people keep re-asking: data store, messaging, auth, layering."),
            new(Area, "At least one ADR is accepted", 3, accepted >= 1,
                "Set `Status: Accepted` on decisions that are in force, so agents know which ones apply."),
        ];
    }

    /// <summary>ADR files in docs/adr, not counting the 0000 template.</summary>
    public static IReadOnlyList<string> RealAdrs(IRepoFiles repo) => repo.Files("docs/adr")
        .Where(p => AdrFile().IsMatch(System.IO.Path.GetFileName(p)) && !System.IO.Path.GetFileName(p).StartsWith("0000", StringComparison.Ordinal))
        .ToList();

    [GeneratedRegex(@"^\d{4}-.+\.md$")]
    private static partial Regex AdrFile();

    [GeneratedRegex(@"status\W+accepted", RegexOptions.IgnoreCase)]
    private static partial Regex AcceptedStatus();
}
