using System.Text.Json;
using AiReadiness.Scanner.Files;

namespace AiReadiness.Scanner.Tests;

public sealed class TempDir : IDisposable
{
    public TempDir() => Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ai-readiness-" + Guid.NewGuid().ToString("N"));

    public string Path { get; }

    public TempDir Write(string relative, string content)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return this;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

public class DiskRepoFilesTests
{
    [Fact]
    public void Files_SkipsGitAndBuildFolders_AndUsesForwardSlashes()
    {
        using var dir = new TempDir().Write("docs/adr/0001-a.md", "x").Write(".git/config", "x").Write("src/bin/a.dll", "x").Write("README.md", "x");

        var files = new DiskRepoFiles(dir.Path).Files(string.Empty);

        Assert.Equal(["README.md", "docs/adr/0001-a.md"], files);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("docs/../../outside.txt")]
    public void ReadText_OutsideTheRoot_ReturnsNull(string path)
    {
        using var dir = new TempDir().Write("inside.txt", "x");
        File.WriteAllText(System.IO.Path.Combine(dir.Path, "..", "outside.txt"), "secret");

        try
        {
            Assert.Null(new DiskRepoFiles(dir.Path).ReadText(path));
        }
        finally
        {
            File.Delete(System.IO.Path.Combine(dir.Path, "..", "outside.txt"));
        }
    }

    [Fact]
    public void ReadText_ThroughSymbolicLink_ReturnsNull()
    {
        using var dir = new TempDir().Write("real.txt", "x");
        try
        {
            File.CreateSymbolicLink(System.IO.Path.Combine(dir.Path, "link.txt"), System.IO.Path.Combine(dir.Path, "real.txt"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        var repo = new DiskRepoFiles(dir.Path);

        Assert.Equal("x", repo.ReadText("real.txt"));
        Assert.Null(repo.ReadText("link.txt"));
    }

    [Fact]
    public void Constructor_WithMissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => new DiskRepoFiles(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}

public class ScannerCliTests
{
    private static (int Code, string Out, string Err) Run(FakeRepo repo, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = ScannerCli.Run(args, output, error, _ => repo);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Run_Default_PrintsMarkdownWithScoreAndBiggestGapFirst()
    {
        var (code, output, _) = Run(new FakeRepo());

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("# AI-readiness: 14/100 (Not ready)", output);
        Assert.Contains("1. **+6** AGENTS.md exists", output);
    }

    [Fact]
    public void Run_WithJson_PrintsParseableReport()
    {
        var (_, output, _) = Run(new FakeRepo(), "--json");

        using var doc = JsonDocument.Parse(output);
        Assert.Equal(14, doc.RootElement.GetProperty("score").GetInt32());
        Assert.Equal(100, doc.RootElement.GetProperty("max").GetInt32());
    }

    [Theory]
    [InlineData("15", ScannerCli.BelowMinimum)]
    [InlineData("14", ScannerCli.Ok)]
    public void Run_WithMinScore_ExitCodeFollowsScore(string min, int expected)
    {
        Assert.Equal(expected, Run(new FakeRepo(), "--min-score", min).Code);
    }

    [Theory]
    [InlineData("--min-score")]
    [InlineData("--min-score abc")]
    [InlineData("--min-score 101")]
    [InlineData("--nope")]
    public void Run_WithBadArguments_ReturnsUsageError(string commandLine)
    {
        var (code, _, error) = Run(new FakeRepo(), commandLine.Split(' '));

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Run_WithMissingFolder_ReportsItAndReturnsUsageError()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = ScannerCli.Run(["/definitely/not/here"], output, error);

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.Contains("Folder not found", error.ToString());
    }

    [Fact]
    public void Run_OnDiskRepository_ScoresRealFiles()
    {
        using var dir = new TempDir().Write("AGENTS.md", Sample.Agents).Write("CLAUDE.md", "@AGENTS.md");
        var output = new StringWriter();

        var code = ScannerCli.Run([dir.Path, "--min-score", "30"], output, new StringWriter());

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("Agent context (AGENTS.md) | 20/20", output.ToString());
    }
}
