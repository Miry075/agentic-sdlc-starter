using System.Text.RegularExpressions;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed partial class AgentContextCheck : IReadinessCheck
{
    private const int MaxLines = 300;

    public string Area => "Agent context (AGENTS.md)";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var text = repo.ReadText("AGENTS.md");
        var claude = repo.ReadText("CLAUDE.md");

        return
        [
            new(Area, "AGENTS.md exists", 6, text is not null,
                "Add AGENTS.md at the repository root: what the repo is, how to build and test it, the layout, and what agents must never do."),
            new(Area, "Build and test commands are documented", 4, text is not null && CommandPattern().IsMatch(text),
                "Document the exact build and test commands in AGENTS.md, for example `dotnet build` and `dotnet test`."),
            new(Area, "Boundaries are stated (never / do not)", 4, text is not null && BoundaryPattern().IsMatch(text),
                "Add a section that says what agents must never do, such as touching secrets, migrations or generated code."),
            new(Area, $"Short enough to be read every session (up to {MaxLines} lines)", 2,
                text is not null && text.Split('\n').Length <= MaxLines,
                $"Cut AGENTS.md to {MaxLines} lines or fewer. Move detail into docs/ and link to it."),
            new(Area, "No unfilled template placeholders", 2, text is not null && !PlaceholderPattern().IsMatch(text),
                "Replace the template placeholders such as <team name> or <YYYY-MM-DD> with real values."),
            new(Area, "CLAUDE.md points to AGENTS.md", 2, claude is not null && claude.Contains("@AGENTS.md", StringComparison.Ordinal),
                "Create CLAUDE.md containing the single line `@AGENTS.md` so one file serves every agent."),
        ];
    }

    [GeneratedRegex(@"(dotnet\s+(build|test)|npm\s+(test|run)|mvn\s|gradle|pytest|go\s+test|cargo\s+test)", RegexOptions.IgnoreCase)]
    private static partial Regex CommandPattern();

    [GeneratedRegex(@"\b(never|must not|do not|don't)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BoundaryPattern();

    [GeneratedRegex(@"(?<!\w)<(?:[A-Za-z][A-Za-z ]{2,}|YYYY-MM-DD)>")]
    private static partial Regex PlaceholderPattern();
}
