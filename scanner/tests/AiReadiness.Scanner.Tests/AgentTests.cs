using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;
using AiReadiness.Scanner.Remediation.Fixes;

namespace AiReadiness.Scanner.Tests;

internal sealed class FakeAgent(bool available = true, int exitCode = 0, Action<string>? onRun = null, string[]? output = null) : IAgentRunner
{
    public List<(string Directory, IReadOnlyList<string> Arguments)> Runs { get; } = [];

    public bool IsAvailable() => available;

    public int Run(string workingDirectory, IReadOnlyList<string> arguments, Action<string> onOutputLine)
    {
        Runs.Add((workingDirectory, arguments));
        foreach (var line in output ?? [])
        {
            onOutputLine(line);
        }

        onRun?.Invoke(workingDirectory);
        return exitCode;
    }
}

public class AgentCommandTests
{
    [Fact]
    public void Arguments_RunTheSkillUnattendedWithOurLoginAndNoPrompts()
    {
        var id = Guid.NewGuid();
        var args = AgentCommand.Arguments(id);

        Assert.Equal(["-p", "/ai-readiness unattended", "--session-id", id.ToString()], args.Take(4));
        Assert.Contains("acceptEdits", args);
        Assert.DoesNotContain("--permission-prompts", args);
        Assert.Contains("stream-json", args);
        Assert.Contains("--verbose", args);
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
        var display = AgentCommand.Display(Guid.NewGuid());

        Assert.Contains("'/ai-readiness unattended'", display);
        Assert.StartsWith("claude -p ", display);
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
        Assert.Contains("claude -p '/ai-readiness unattended' --session-id ", output);
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
        var arguments = Assert.Single(agent.Runs).Arguments;
        var sessionId = Guid.Parse(arguments[arguments.ToList().IndexOf("--session-id") + 1]);
        Assert.Equal(AgentCommand.Arguments(sessionId), arguments);
        Assert.Contains("AI-readiness: 14/100 to 34/100", output);
        Assert.Contains($"claude --resume {sessionId}", output);
    }

    [Fact]
    public void Agent_KeepsAnExistingSkill()
    {
        var repo = GitRepo().With(ReadinessSkillFix.Path, "my version");

        Run(repo, new FakeAgent());

        Assert.Equal("my version", repo.ReadText(ReadinessSkillFix.Path));
    }

    [Fact]
    public void Agent_WhenClaudeFails_ReturnsAgentFailedWithoutAResumeHint()
    {
        var (code, output, error) = Run(GitRepo(), new FakeAgent(exitCode: 1));

        Assert.Equal(ScannerCli.AgentFailed, code);
        Assert.Contains("claude update", error);
        Assert.DoesNotContain("claude --resume", output);
    }

    [Fact]
    public void Agent_WithUnknownOption_ReturnsUsageError()
    {
        Assert.Equal(ScannerCli.UsageError, Run(GitRepo(), new FakeAgent(), "--yolo").Code);
    }

    [Fact]
    public void Agent_WithTwoPaths_ReturnsUsageError()
    {
        Assert.Equal(ScannerCli.UsageError, Run(GitRepo(), new FakeAgent(), "one", "two").Code);
    }

    [Fact]
    public void Agent_LogsEachStepAsItArrives()
    {
        var agent = new FakeAgent(output:
        [
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Write\",\"input\":{\"file_path\":\"AGENTS.md\"}}]}}",
        ]);

        var (_, output, _) = Run(GitRepo(), agent);

        Assert.Contains("write  AGENTS.md", output);
        Assert.Matches(new System.Text.RegularExpressions.Regex(@"\[\d\d:\d\d:\d\d\] write  AGENTS\.md"), output);
    }
}

public class AgentProgressTests
{
    /// <summary>JSON written with single quotes, for readability.</summary>
    private static string J(string singleQuoted) => singleQuoted.Replace('\'', '"');

    [Fact]
    public void Describe_Init_SaysClaudeStartedWithTheModel()
    {
        Assert.Equal(["Claude Code started (claude-opus-5-5)"], AgentProgress.Describe(J("{'type':'system','subtype':'init','model':'claude-opus-5-5'}")));
    }

    [Theory]
    [InlineData("Bash", "{'command':'git switch -c ai-readiness/2026-10-16\\nmore'}", "$ git switch -c ai-readiness/2026-10-16")]
    [InlineData("Read", "{'file_path':'src/Api/Program.cs'}", "read   src/Api/Program.cs")]
    [InlineData("Edit", "{'file_path':'AGENTS.md'}", "edit   AGENTS.md")]
    [InlineData("Grep", "{'pattern':'class .*Order'}", "search class .*Order")]
    [InlineData("Skill", "{'skill':'ai-readiness'}", "skill  ai-readiness")]
    [InlineData("WebFetch", "{}", "tool   WebFetch")]
    public void Describe_ToolUse_ShowsWhatTheStepTouches(string tool, string input, string expected)
    {
        var line = J("{'type':'assistant','message':{'content':[{'type':'tool_use','name':'" + tool + "','input':" + input + "}]}}");

        Assert.Equal([expected], AgentProgress.Describe(line));
    }

    [Fact]
    public void Describe_AssistantText_IsIndentedLineByLine()
    {
        var line = J("{'type':'assistant','message':{'content':[{'type':'text','text':'Reading the solution.\\n\\nFound 3 projects.'}]}}");

        Assert.Equal(["  Reading the solution.", "  Found 3 projects."], AgentProgress.Describe(line));
    }

    [Fact]
    public void Describe_RefusedTool_IsFlagged()
    {
        var line = J("{'type':'user','message':{'content':[{'type':'tool_result','is_error':true,'content':'Permission to use Bash with command git push has been denied.'}]}}");

        Assert.Equal(["  ! Permission to use Bash with command git push has been denied."], AgentProgress.Describe(line));
    }

    [Fact]
    public void Describe_SuccessfulToolResult_IsQuiet()
    {
        Assert.Empty(AgentProgress.Describe(J("{'type':'user','message':{'content':[{'type':'tool_result','content':'ok'}]}}")));
    }

    [Fact]
    public void Describe_Result_ShowsDurationTurnsAndTheFinalMessage()
    {
        var lines = AgentProgress.Describe(J("{'type':'result','subtype':'success','is_error':false,'duration_ms':125000,'num_turns':42,'result':'Score 32 to 71.\\nQuestions for you:\\n1. What is a lot?'}"));

        Assert.Equal("Claude Code finished in 125s, 42 turns", lines[0]);
        Assert.Contains("1. What is a lot?", lines);
    }

    [Fact]
    public void Describe_NonJsonLine_IsShownAsIs()
    {
        Assert.Equal(["error: something odd"], AgentProgress.Describe("error: something odd"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{'type':'stream_event'}")]
    public void Describe_NoiseIsDropped(string line)
    {
        Assert.Empty(AgentProgress.Describe(J(line)));
    }
}
