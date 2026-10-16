namespace AiReadiness.Scanner.Domain;

public sealed record RepoResult(string Name, Assessment Assessment);

/// <summary>The scores of several repositories, worst first, so the work to do is at the top.</summary>
public sealed class OrgAssessment
{
    public OrgAssessment(IEnumerable<RepoResult> results)
    {
        Ranked = results
            .OrderBy(r => r.Assessment.Score)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<RepoResult> Ranked { get; }

    public int AverageScore => Ranked.Count == 0 ? 0 : (int)Math.Round(Ranked.Average(r => r.Assessment.Score), MidpointRounding.AwayFromZero);

    public int Count(ReadinessBand band) => Ranked.Count(r => r.Assessment.Band == band);

    public bool AnyBelow(int minimum) => Ranked.Any(r => r.Assessment.Score < minimum);
}
