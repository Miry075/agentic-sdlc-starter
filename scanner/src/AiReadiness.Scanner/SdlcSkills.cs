namespace AiReadiness.Scanner;

/// <summary>The SDLC phase skills `ai-readiness sdlc` installs into a repository, from the starter's templates.</summary>
public static class SdlcSkills
{
    public static IReadOnlyList<string> Names { get; } =
        [
            "sdlc", "requirements-phase", "design-phase", "build-phase", "test-phase", "deliver-phase", "release-phase",
            "azure-workitem-flow", "dotnet-change-guardrails", "dotnet-design-principles", "dotnet-testing-standards", "resolve-review-feedback",
        ];

    public static string RepoPath(string name) => $".claude/skills/{name}/SKILL.md";

    public static string AssetName(string name) => $"assets/skills/{name}.md";
}
