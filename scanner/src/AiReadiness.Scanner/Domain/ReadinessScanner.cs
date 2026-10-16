namespace AiReadiness.Scanner.Domain;

public sealed class ReadinessScanner(IEnumerable<IReadinessCheck> checks)
{
    public Assessment Scan(IRepoFiles repo) =>
        new(checks.SelectMany(check => check.Evaluate(repo)));
}
