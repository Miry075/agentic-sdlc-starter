namespace AiReadiness.Scanner;

/// <summary>
/// The Claude Code call behind `ai-readiness agent`: runs the /ai-readiness skill unattended with the
/// smallest set of permissions it needs. No push, no network tools, nothing outside these commands.
/// </summary>
public static class AgentCommand
{
    public const string Prompt = "/ai-readiness unattended";

    public static IReadOnlyList<string> AllowedTools { get; } =
    [
        "Bash(ai-readiness *)",
        "Bash(command -v *)",
        "Bash(git status *)",
        "Bash(git switch *)",
        "Bash(git add *)",
        "Bash(git commit *)",
        "Bash(git log *)",
        "Bash(git branch *)",
        "Bash(git diff *)",
        "Bash(dotnet build *)",
        "Bash(dotnet test *)",
        "Bash(bash scripts/check-docs-templates.sh *)",
    ];

    public static IReadOnlyList<string> Arguments { get; } =
    [
        "-p", Prompt,
        "--permission-mode", "acceptEdits",
        "--allowedTools", string.Join(",", AllowedTools),
        "--permission-prompts", "none",
    ];

    /// <summary>The command as you would type it, for --dry-run.</summary>
    public static string Display() =>
        "claude " + string.Join(" ", Arguments.Select(a => a.Any(c => char.IsWhiteSpace(c) || c is '*' or '(' or ')' or ',') ? $"'{a}'" : a));
}
