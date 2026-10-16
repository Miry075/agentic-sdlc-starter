using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Tests;

/// <summary>In-memory repository: the fake for the IRepoFiles port.</summary>
internal sealed class FakeRepo : IRepoFiles
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public FakeRepo With(string path, string content = "")
    {
        _files[path] = content;
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => _files.Keys.Any(k => k.StartsWith(path + "/", StringComparison.Ordinal));

    public string? ReadText(string path) => _files.GetValueOrDefault(path);

    public IReadOnlyList<string> Files(string directory) => _files.Keys
        .Where(k => directory.Length == 0 || k.StartsWith(directory + "/", StringComparison.Ordinal))
        .OrderBy(k => k, StringComparer.Ordinal)
        .ToList();
}

internal static class Sample
{
    public const string Agents = """
        # AGENTS.md
        > **Owner:** Billing team · **Last reviewed:** 2026-10-01

        ## Build, test, run
        ```bash
        dotnet build
        dotnet test
        ```

        ## Never
        - Never edit generated code.
        """;

    public static string Glossary(int terms) =>
        "| Term | Meaning |\n|---|---|\n" + string.Concat(Enumerable.Range(1, terms).Select(i => $"| **Term{i}** | Meaning {i} |\n"));

    public static string Questions(int count) =>
        "questions:\n" + string.Concat(Enumerable.Range(1, count).Select(i => $"  - id: q{i:000}\n    question: \"Q{i}?\"\n"));

    public static IReadOnlyList<Criterion> Run(IReadinessCheck check, FakeRepo repo) => check.Evaluate(repo);

    public static bool Passed(IReadOnlyList<Criterion> criteria, string name) =>
        criteria.Single(c => c.Name.StartsWith(name, StringComparison.Ordinal)).Passed;
}
