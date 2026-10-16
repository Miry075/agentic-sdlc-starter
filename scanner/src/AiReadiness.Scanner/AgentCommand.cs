namespace AiReadiness.Scanner;

/// <summary>
/// The Claude Code call behind `ai-readiness agent`: runs the /ai-readiness skill unattended with the
/// smallest set of permissions it needs. No push, no network tools, nothing outside these commands.
/// In a `claude -p` run nobody can answer a permission prompt, so anything not allowed here is denied.
/// (No --permission-prompts flag: it needs Claude Code v2.1.259+ and adds nothing for a plain -p run.)
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

    /// <summary>
    /// The run gets a known session ID, because `claude --continue` skips `claude -p` sessions:
    /// the only reliable way back into this run is `claude --resume <id>`.
    /// </summary>
    public static IReadOnlyList<string> Arguments(Guid sessionId) =>
    [
        "-p", Prompt,
        "--session-id", sessionId.ToString(),
        "--output-format", "stream-json",
        "--verbose",
        "--permission-mode", "acceptEdits",
        "--allowedTools", string.Join(",", AllowedTools),
    ];

    public static string ResumeCommand(Guid sessionId) => $"claude --resume {sessionId}";

    /// <summary>The command as you would type it, for --dry-run.</summary>
    public static string Display(Guid sessionId) =>
        "claude " + string.Join(" ", Arguments(sessionId).Select(a => a.Any(c => char.IsWhiteSpace(c) || c is '*' or '(' or ')' or ',') ? $"'{a}'" : a));
}
