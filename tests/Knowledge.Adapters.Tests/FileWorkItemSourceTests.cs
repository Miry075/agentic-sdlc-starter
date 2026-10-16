using Knowledge.Adapters.Dev;
using Knowledge.Core;

namespace Knowledge.Adapters.Tests;

public class FileWorkItemSourceTests
{
    private static readonly CallerContext Caller = new("dev@localhost", new HashSet<string> { "developers" }, "Dev");

    [Fact]
    public async Task GetAsync_ForExistingFile_ReturnsTheWorkItem()
    {
        using var folder = new TempFolder();
        folder.Write("42.json", """{ "id": 42, "title": "Issue credit note", "state": "Active", "description": "d" }""");

        var item = await new FileWorkItemSource(folder.Path).GetAsync(Caller, 42, CancellationToken.None);

        Assert.NotNull(item);
        Assert.Equal("Active", item.State);
        Assert.Empty(item.Comments);
        Assert.Empty(item.Links);
    }

    [Fact]
    public async Task GetAsync_ForMissingFile_ReturnsNull()
    {
        using var folder = new TempFolder();

        Assert.Null(await new FileWorkItemSource(folder.Path).GetAsync(Caller, 99, CancellationToken.None));
    }
}
