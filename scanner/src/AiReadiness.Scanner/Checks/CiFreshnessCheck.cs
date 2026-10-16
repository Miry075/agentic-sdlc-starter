using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed class CiFreshnessCheck : IReadinessCheck
{
    public string Area => "Keeping context fresh in CI";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var pipelines = PipelineFiles(repo);
        var contents = pipelines.Select(p => repo.ReadText(p) ?? string.Empty).ToList();

        return
        [
            new(Area, "A CI pipeline exists", 3, pipelines.Count > 0,
                "Add a pipeline under .github/workflows or an azure-pipelines.yml that runs on pull requests."),
            new(Area, "CI fails when the structure changes without AGENTS.md", 4,
                contents.Any(c => c.Contains("check-agents-md-freshness", StringComparison.OrdinalIgnoreCase)),
                "Copy scripts/check-agents-md-freshness.sh from the starter and run it on pull requests."),
            new(Area, "CI checks that ADRs and specs follow their template", 3,
                contents.Any(c => c.Contains("check-docs-templates", StringComparison.OrdinalIgnoreCase)),
                "Run scripts/check-docs-templates.sh on pull requests so decisions and specs keep their structure."),
        ];
    }

    private static List<string> PipelineFiles(IRepoFiles repo)
    {
        var files = repo.Files(".github/workflows")
            .Concat(repo.Files(".azure-pipelines"))
            .Where(IsYaml)
            .ToList();
        files.AddRange(new[] { "azure-pipelines.yml", "azure-pipelines.yaml" }.Where(repo.FileExists));
        return files;
    }

    private static bool IsYaml(string path) =>
        path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);
}
