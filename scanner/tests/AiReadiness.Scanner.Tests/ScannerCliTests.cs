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

internal sealed class FakeBrowser(bool succeeds = true) : IBrowserLauncher
{
    public List<string> Opened { get; } = [];

    public bool TryOpen(string path)
    {
        Opened.Add(path);
        return succeeds;
    }
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
    [InlineData("--out")]
    [InlineData("--cta-text hello")]
    [InlineData("--cta-url javascript:alert(1)")]
    [InlineData("--cta-url not-a-url")]
    [InlineData("--apply")]
    [InlineData("--fix --all")]
    [InlineData("--fix --html")]
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

        var (code, output, error) = Run("--out", file, "--cta-url", "https://example.test/book", "--cta-text", "Book a call");

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

    private static (int Code, string Out, string Err) RunWithBrowser(FakeBrowser? browser, IRepoOpener opener, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = new ScannerCli(opener, Clock, new EmbeddedAssets(), browser).Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Run_WithBrowser_WritesTheReportToATempFileAndOpensIt()
    {
        var browser = new FakeBrowser();

        var (code, _, error) = RunWithBrowser(browser, new FakeOpener());

        Assert.Equal(ScannerCli.Ok, code);
        var path = Assert.Single(browser.Opened);
        try
        {
            Assert.Contains("repo-20261016-080000.html", path);
            Assert.Contains("AI-readiness report", File.ReadAllText(path));
            Assert.Contains(path, error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_WithExplicitHtmlPath_OpensThatFile()
    {
        using var dir = new TempDir();
        var file = System.IO.Path.Combine(dir.Path, "mine.html");
        var browser = new FakeBrowser();

        RunWithBrowser(browser, new FakeOpener(), "--out", file);

        Assert.Equal([file], browser.Opened);
        Assert.True(File.Exists(file));
    }

    [Theory]
    [InlineData("--no-open")]
    [InlineData("--json")]
    public void Run_WhenOpeningIsOffOrOutputIsJson_DoesNotOpenOrWriteAnything(string option)
    {
        var browser = new FakeBrowser();

        var (code, _, error) = RunWithBrowser(browser, new FakeOpener(), option);

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Empty(browser.Opened);
        Assert.DoesNotContain("HTML report written", error);
    }

    [Fact]
    public void Run_WithHtmlFlagAndNoFile_WritesATempReportEvenWithoutABrowser()
    {
        var (code, _, error) = RunWithBrowser(null, new FakeOpener(), "--html");

        Assert.Equal(ScannerCli.Ok, code);
        var line = error.Split('\n').Single(l => l.StartsWith("HTML report written to ", StringComparison.Ordinal));
        var path = line["HTML report written to ".Length..].Trim();
        try
        {
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_WithHtmlFlagAndNoOpen_WritesTheReportButDoesNotOpenIt()
    {
        var browser = new FakeBrowser();

        var (_, _, error) = RunWithBrowser(browser, new FakeOpener(), "--html", "--no-open");

        Assert.Empty(browser.Opened);
        Assert.Contains("HTML report written to", error);
        var path = error.Split('\n').Single(l => l.StartsWith("HTML report written to ", StringComparison.Ordinal))["HTML report written to ".Length..].Trim();
        File.Delete(path);
    }

    [Fact]
    public void Run_WithoutABrowser_WritesNoHtmlUnlessAsked()
    {
        var (code, _, error) = RunWithBrowser(null, new FakeOpener());

        Assert.Equal(ScannerCli.Ok, code);
        Assert.DoesNotContain("HTML report written", error);
    }

    [Fact]
    public void Run_WhenTheBrowserCannotBeStarted_WarnsAndStillSucceeds()
    {
        var browser = new FakeBrowser(succeeds: false);

        var (code, output, error) = RunWithBrowser(browser, new FakeOpener(), "--min-score", "0");

        try
        {
            Assert.Equal(ScannerCli.Ok, code);
            Assert.Contains("# AI-readiness", output);
            Assert.Contains("Could not open the browser", error);
        }
        finally
        {
            browser.Opened.ForEach(File.Delete);
        }
    }

    [Fact]
    public void Run_All_OpensTheOrganisationReport()
    {
        var browser = new FakeBrowser();

        RunWithBrowser(browser, new FakeOpener(("a", new FakeRepo()), ("b", new FakeRepo())), "--all", "/work");

        var path = Assert.Single(browser.Opened);
        try
        {
            Assert.Contains("2 repositories", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_Fix_NeverOpensTheBrowser()
    {
        var browser = new FakeBrowser();

        RunWithBrowser(browser, new FakeOpener(), "--fix");

        Assert.Empty(browser.Opened);
    }

    [Fact]
    public void Run_FixJson_GivesToolsThePlanWithPromptsAndCriteria()
    {
        var (code, output, _) = Run(new FakeOpener(("repo", new FakeRepo())), "--fix", "--json");

        Assert.Equal(ScannerCli.Ok, code);
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("applied").GetBoolean());
        Assert.Equal(14, root.GetProperty("before").GetInt32());
        var agents = root.GetProperty("guided").EnumerateArray().Single(g => g.GetProperty("title").GetString() == "Write AGENTS.md");
        Assert.Contains("Do not invent", agents.GetProperty("prompt").GetString());
        Assert.Contains(agents.GetProperty("criteria").EnumerateArray(), c => c.GetString() == "AGENTS.md exists");
    }

    [Fact]
    public void Run_FixApplyJson_ReportsTheScoreAfterWriting()
    {
        var repo = new FakeRepo();

        var (_, output, _) = Run(new FakeOpener(("repo", repo)), "--fix", "--apply", "--json");

        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.GetProperty("applied").GetBoolean());
        Assert.Equal(doc.RootElement.GetProperty("afterAutomatic").GetInt32(), doc.RootElement.GetProperty("score").GetInt32());
        Assert.True(repo.FileExists(".claude/skills/ai-readiness/SKILL.md"));
    }
}

public class AutoOpenTests
{
    [Theory]
    [InlineData(false, null, true)]
    [InlineData(true, null, false)]
    [InlineData(false, "CI", false)]
    [InlineData(false, "TF_BUILD", false)]
    [InlineData(false, "GITHUB_ACTIONS", false)]
    public void IsAllowed_OnlyAtATerminalOutsideCi(bool redirected, string? ciVariable, bool expected)
    {
        var allowed = AutoOpen.IsAllowed(redirected, name => name == ciVariable ? "true" : null);

        Assert.Equal(expected, allowed);
    }
}
