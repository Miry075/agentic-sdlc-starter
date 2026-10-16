using System.Text.Json;
using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;

namespace AiReadiness.Scanner.Tests;

internal sealed class FakeOpener(params (string Name, FakeRepo Repo)[] repos) : IRepoOpener
{
    private readonly FakeRepo _single = repos.FirstOrDefault().Repo ?? new FakeRepo();

    public RepoEntry Open(string path) => new("repo", _single);

    public IRepoWriter OpenWriter(string path) => _single;

    public IReadOnlyList<RepoEntry> OpenAll(string parent) => repos.Select(r => new RepoEntry(r.Name, r.Repo)).ToList();
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class ScannerCliTests
{
    private static readonly TimeProvider Clock = new FixedClock(new DateTimeOffset(2026, 10, 16, 8, 0, 0, TimeSpan.Zero));

    private static (int Code, string Out, string Err) Run(IRepoOpener opener, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = new ScannerCli(opener, Clock, new EmbeddedAssets()).Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static (int Code, string Out, string Err) Run(params string[] args) => Run(new FakeOpener(), args);

    private static FakeRepo Complete() => new FakeRepo()
        .With("AGENTS.md", Sample.Agents)
        .With("CLAUDE.md", "@AGENTS.md")
        .With("docs/domain/glossary.md", Sample.Glossary(15));

    [Fact]
    public void Run_Default_PrintsMarkdownWithScoreAndBiggestGapFirst()
    {
        var (code, output, _) = Run();

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("# AI-readiness: 14/100 (Not ready)", output);
        Assert.Contains("1. **+6** AGENTS.md exists", output);
    }

    [Fact]
    public void Run_WithJson_PrintsParseableReport()
    {
        var (_, output, _) = Run("--json");

        using var doc = JsonDocument.Parse(output);
        Assert.Equal(14, doc.RootElement.GetProperty("score").GetInt32());
        Assert.Equal(100, doc.RootElement.GetProperty("max").GetInt32());
    }

    [Theory]
    [InlineData("15", ScannerCli.BelowMinimum)]
    [InlineData("14", ScannerCli.Ok)]
    public void Run_WithMinScore_ExitCodeFollowsScore(string min, int expected)
    {
        Assert.Equal(expected, Run("--min-score", min).Code);
    }

    [Theory]
    [InlineData("--min-score")]
    [InlineData("--min-score abc")]
    [InlineData("--min-score 101")]
    [InlineData("--nope")]
    [InlineData("--html")]
    [InlineData("--cta-text hello")]
    [InlineData("--cta-url javascript:alert(1)")]
    [InlineData("--cta-url not-a-url")]
    [InlineData("--apply")]
    [InlineData("--fix --all")]
    [InlineData("--fix --json")]
    public void Run_WithBadArguments_ReturnsUsageError(string commandLine)
    {
        var (code, _, error) = Run(commandLine.Split(' '));

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Run_WithMissingFolder_ReportsItAndReturnsUsageError()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = new ScannerCli(new DiskRepoOpener(), Clock, new EmbeddedAssets()).Run(["/definitely/not/here"], output, error);

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.Contains("Folder not found", error.ToString());
    }

    [Fact]
    public void Run_OnDiskRepository_ScoresRealFiles()
    {
        using var dir = new TempDir().Write("AGENTS.md", Sample.Agents).Write("CLAUDE.md", "@AGENTS.md");
        var output = new StringWriter();

        var code = new ScannerCli(new DiskRepoOpener(), Clock, new EmbeddedAssets()).Run([dir.Path, "--min-score", "30"], output, new StringWriter());

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("Agent context (AGENTS.md) | 20/20", output.ToString());
    }

    [Fact]
    public void Run_WithHtml_WritesStandaloneReportAndKeepsStdoutClean()
    {
        using var dir = new TempDir();
        var file = System.IO.Path.Combine(dir.Path, "report.html");
        Directory.CreateDirectory(dir.Path);

        var (code, output, error) = Run("--html", file, "--cta-url", "https://example.test/book", "--cta-text", "Book a call");

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("# AI-readiness", output);
        Assert.Contains("report.html", error);
        var html = File.ReadAllText(file);
        Assert.Contains("2026-10-16", html);
        Assert.Contains("href=\"https://example.test/book\"", html);
        Assert.Contains("Book a call", html);
    }

    [Fact]
    public void Run_All_RanksRepositoriesLowestFirstWithAverage()
    {
        var opener = new FakeOpener(("good-repo", Complete()), ("empty-repo", new FakeRepo()));

        var (code, output, _) = Run(opener, "--all", "/work");

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("across 2 repositories: average", output);
        Assert.True(output.IndexOf("empty-repo", StringComparison.Ordinal) < output.IndexOf("good-repo", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_AllWithMinScore_FailsWhenAnyRepositoryIsBelow()
    {
        var opener = new FakeOpener(("good-repo", Complete()), ("empty-repo", new FakeRepo()));

        Assert.Equal(ScannerCli.BelowMinimum, Run(opener, "--all", "/work", "--min-score", "40").Code);
    }

    [Fact]
    public void Run_AllWithNoRepositories_ReturnsUsageError()
    {
        var (code, _, error) = Run(new FakeOpener(), "--all", "/work");

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.Contains("No Git repositories", error);
    }

    [Fact]
    public void Run_Fix_PreviewsWithoutWritingAnything()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents);

        var (code, output, _) = Run(new FakeOpener(("repo", repo)), "--fix");

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("Nothing has been written", output);
        Assert.Contains("create `CLAUDE.md`", output);
        Assert.False(repo.FileExists("CLAUDE.md"));
    }

    [Fact]
    public void Run_FixApply_CreatesFilesAndReportsTheNewScore()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents);

        var (code, output, _) = Run(new FakeOpener(("repo", repo)), "--fix", "--apply");

        Assert.Equal(ScannerCli.Ok, code);
        Assert.True(repo.FileExists("CLAUDE.md"));
        Assert.True(repo.FileExists(".claude/settings.json"));
        Assert.Contains("# Applied to `repo`:", output);
    }

    [Fact]
    public void Run_FixApplyTwice_SecondRunHasNothingToCreate()
    {
        var opener = new FakeOpener(("repo", new FakeRepo().With("AGENTS.md", Sample.Agents)));
        Run(opener, "--fix", "--apply");

        var (code, output, _) = Run(opener, "--fix", "--apply");

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("Nothing the scanner can safely create", output);
    }
}
