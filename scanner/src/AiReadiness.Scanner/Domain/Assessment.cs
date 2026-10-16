namespace AiReadiness.Scanner.Domain;

/// <summary>One yes/no question about a repository, worth <see cref="Weight"/> points.</summary>
public sealed record Criterion(string Area, string Name, int Weight, bool Passed, string Advice)
{
    public int Points => Passed ? Weight : 0;
}

public interface IReadinessCheck
{
    string Area { get; }

    IReadOnlyList<Criterion> Evaluate(IRepoFiles repo);
}

public enum ReadinessBand
{
    NotReady,
    Partial,
    Ready,
    Strong,
}

public sealed class Assessment
{
    public Assessment(IEnumerable<Criterion> criteria)
    {
        Criteria = criteria.ToList();
    }

    public IReadOnlyList<Criterion> Criteria { get; }

    public int Score => Criteria.Sum(c => c.Points);

    public int MaxScore => Criteria.Sum(c => c.Weight);

    public ReadinessBand Band => Score switch
    {
        >= 90 => ReadinessBand.Strong,
        >= 70 => ReadinessBand.Ready,
        >= 40 => ReadinessBand.Partial,
        _ => ReadinessBand.NotReady,
    };

    /// <summary>Failed criteria, the ones worth the most points first.</summary>
    public IReadOnlyList<Criterion> Gaps => Criteria
        .Where(c => !c.Passed)
        .OrderByDescending(c => c.Weight)
        .ToList();

    public IReadOnlyList<(string Area, int Points, int Max)> ByArea => Criteria
        .GroupBy(c => c.Area)
        .Select(g => (g.Key, g.Sum(c => c.Points), g.Sum(c => c.Weight)))
        .ToList();
}
