using System.Text.Json;
using AiReadiness.Scanner.Domain;
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

public class DiskRepoOpenerTests
{
    [Fact]
    public void OpenAll_ReturnsOnlyGitRepositoriesDirectlyInside_SortedByName()
    {
        using var dir = new TempDir()
            .Write("zeta/.git/HEAD", "x")
            .Write("alpha/.git/HEAD", "x")
            .Write("notes/README.md", "x")
            .Write("alpha/nested/.git/HEAD", "x");

        var repos = new DiskRepoOpener().OpenAll(dir.Path);

        Assert.Equal(["alpha", "zeta"], repos.Select(r => r.Name));
    }

    [Fact]
    public void OpenAll_WithMissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => new DiskRepoOpener().OpenAll(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}
