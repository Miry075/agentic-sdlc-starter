using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;
using AiReadiness.Scanner.Remediation.Fixes;

namespace AiReadiness.Scanner.Tests;

internal sealed class FakeAgent(bool available = true, int exitCode = 0, Action<string>? onRun = null) : IAgentRunner
{
    public List<(string Directory, IReadOnlyList<string> Arguments)> Runs { get; } = [];

    public bool IsAvailable() => available;

    public int Run(string workingDirectory, IReadOnlyList<string> arguments)
    {
        Runs.Add((workingDirectory, arguments));
        onRun?.Invoke(workingDirectory);
        return exitCode;
    }
}

public class AgentCommandTests
{
    [Fact]
    public void Arguments_RunTheSkillUnattendedWithOurLoginAndNoPrompts()
    {
        var args = AgentCommand.Arguments;

        Assert.Equal(["-p", "/ai-readiness unattended"], args.Take(2));
        Assert.Contains("acceptEdits", args);
        Assert.Contains("none", args);
        Assert.DoesNotContain("--bare", args);
    }

    [Fact]
    public void AllowedTools_NeverIncludePushOrUnrestrictedShell()
    {
        Assert.DoesNotContain(AgentCommand.AllowedTools, t => t.Contains("push", StringComparison.Ordinal));
        Assert.DoesNotContain(AgentCommand.AllowedTools, t => t is "Bash" or "Bash(*)" or "Bash(git *)");
    }

    [Fact]
    public void Display_QuotesArgumentsAShellWouldSplit()
    {
        Assert.Contains("'/ai-readiness unattended'", AgentCommand.Display());
        Assert.StartsWith("claude -p ", AgentCommand.Display());
    }
}

public class ScannerAgentTests
{
    private static readonly TimeProvider Clock = new FixedClock(new DateTimeOffset(2026, 10, 16, 8, 0, 0, TimeSpan.Zero));

    private static (int Code, string Out, string Err) Run(FakeRepo repo, FakeAgent? agent, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = new ScannerCli(new FakeOpener(("repo", repo)), Clock, new EmbeddedAssets(), browser: null, agent).Run(["agent", .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static FakeRepo GitRepo() => new FakeRepo().With(".git/HEAD", "ref: refs/heads/main");

    [Fact]
    public void Agent_OutsideAGitRepository_Refuses()
    {
        var agent = new FakeAgent();

        var (code, _, error) = Run(new FakeRepo(), agent);

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.Contains("Git repository", error);
        Assert.Empty(agent.Runs);
    }

    [Fact]
    public void Agent_WithoutClaudeCode_ExplainsHowToInstallIt()
    {
        var repo = GitRepo();

        var (code, _, error) = Run(repo, new FakeAgent(available: false));

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.Contains("claude", error);
        Assert.False(repo.FileExists(ReadinessSkillFix.Path));
    }

    [Fact]
    public void Agent_DryRun_PrintsTheCommandAndWritesNothing()
    {
        var repo = GitRepo();
        var agent = new FakeAgent();

        var (code, output, _) = Run(repo, agent, "--dry-run");

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains(AgentCommand.Display(), output);
        Assert.Contains("Would install", output);
        Assert.Empty(agent.Runs);
        Assert.False(repo.FileExists(ReadinessSkillFix.Path));
    }

    [Fact]
    public void Agent_InstallsTheSkillRunsClaudeAndReportsTheNewScore()
    {
        var repo = GitRepo();
        var agent = new FakeAgent(onRun: _ => repo.With("AGENTS.md", Sample.Agents).With("CLAUDE.md", "@AGENTS.md"));

        var (code, output, _) = Run(repo, agent);

        Assert.Equal(ScannerCli.Ok, code);
        Assert.True(repo.FileExists(ReadinessSkillFix.Path));
        Assert.Equal(AgentCommand.Arguments, Assert.Single(agent.Runs).Arguments);
        Assert.Contains("AI-readiness: 14/100 to 34/100", output);
        Assert.Contains("claude --continue", output);
    }

    [Fact]
    public void Agent_KeepsAnExistingSkill()
    {
        var repo = GitRepo().With(ReadinessSkillFix.Path, "my version");

        Run(repo, new FakeAgent());

        Assert.Equal("my version", repo.ReadText(ReadinessSkillFix.Path));
    }

    [Fact]
    public void Agent_WhenClaudeFails_ReturnsAgentFailed()
    {
        Assert.Equal(ScannerCli.AgentFailed, Run(GitRepo(), new FakeAgent(exitCode: 1)).Code);
    }

    [Fact]
    public void Agent_WithUnknownOption_ReturnsUsageError()
    {
        Assert.Equal(ScannerCli.UsageError, Run(GitRepo(), new FakeAgent(), "--yolo").Code);
    }
}
