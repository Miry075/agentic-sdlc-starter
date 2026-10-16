using Knowledge.Adapters.RepoContext;

namespace Knowledge.Adapters.Tests;

public class FileSystemRepoSourceTests
{
    [Fact]
    public async Task ReadTextAsync_ForExistingFile_ReturnsItsText()
    {
        using var folder = new TempFolder();
        folder.Write("docs/domain/glossary.md", "# Glossary");

        var text = await new FileSystemRepoSource(folder.Path).ReadTextAsync("docs/domain/glossary.md", CancellationToken.None);

        Assert.Equal("# Glossary", text);
    }

    [Fact]
    public async Task ReadTextAsync_ForMissingFile_ReturnsNull()
    {
        using var folder = new TempFolder();

        Assert.Null(await new FileSystemRepoSource(folder.Path).ReadTextAsync("docs/nothing.md", CancellationToken.None));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("docs/../../secret.txt")]
    [InlineData("docs\\..\\..\\secret.txt")]
    [InlineData("/etc/passwd")]
    public async Task ReadTextAsync_WhenPathLeavesTheRoot_Throws(string path)
    {
        using var folder = new TempFolder();
        var source = new FileSystemRepoSource(folder.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => source.ReadTextAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task ReadTextAsync_ForSymbolicLink_TreatsItAsMissing()
    {
        using var outside = new TempFolder();
        var target = outside.Write("secret.txt", "classified");
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, "docs"));
        File.CreateSymbolicLink(Path.Combine(folder.Path, "docs", "link.md"), target);

        var text = await new FileSystemRepoSource(folder.Path).ReadTextAsync("docs/link.md", CancellationToken.None);

        Assert.Null(text);
    }

    [Fact]
    public async Task ListFilesAsync_ReturnsFilesDirectlyInTheDirectory_SortedWithForwardSlashes()
    {
        using var folder = new TempFolder();
        folder.Write("docs/adr/0002-b.md", "b");
        folder.Write("docs/adr/0001-a.md", "a");
        folder.Write("docs/adr/nested/0003-c.md", "c");

        var files = await new FileSystemRepoSource(folder.Path).ListFilesAsync("docs/adr", CancellationToken.None);

        Assert.Equal(["docs/adr/0001-a.md", "docs/adr/0002-b.md"], files);
    }

    [Fact]
    public async Task ListFilesAsync_ForMissingDirectory_ReturnsEmpty()
    {
        using var folder = new TempFolder();

        Assert.Empty(await new FileSystemRepoSource(folder.Path).ListFilesAsync("docs/adr", CancellationToken.None));
    }
}
