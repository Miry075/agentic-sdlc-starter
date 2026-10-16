using Knowledge.Application.RepoContext;
using Knowledge.Application.Tools;

namespace Knowledge.Application.Tests;

public class AdrCatalogTests
{
    private static string Adr(int n, string title, string status, string decision) => $"""
        # {n:D4}. {title}

        ## Status

        {status}

        ## Context

        Some context.

        ## Decision

        {decision}

        ## Consequences

        Some consequences.
        """;

    private static AdrCatalog Catalog() => new(new[]
    {
        AdrCatalog.ParseRecord("0001-record-decisions.md", Adr(1, "Record architecture decisions", "Accepted", "We write ADRs in the repo."))!,
        AdrCatalog.ParseRecord("0007-use-outbox.md", Adr(7, "Use the outbox pattern for events", "Accepted", "Events are written to an outbox table."))!,
        AdrCatalog.ParseRecord("0009-use-saga.md", Adr(9, "Use a saga for payments", "Proposed", "Payments use a saga."))!,
    });

    [Fact]
    public void ParseRecord_ReadsNumberTitleStatusAndDecision()
    {
        var record = AdrCatalog.ParseRecord("0007-use-outbox.md", Adr(7, "Use the outbox pattern", "Accepted", "Events go to an outbox."));

        Assert.NotNull(record);
        Assert.Equal(7, record.Number);
        Assert.Equal("Use the outbox pattern", record.Title);
        Assert.Equal("Accepted", record.Status);
        Assert.Equal("Events go to an outbox.", record.Decision);
    }

    [Theory]
    [InlineData("0000-template.md")]
    [InlineData("README.md")]
    [InlineData("notes.md")]
    public void ParseRecord_ForTemplateOrOtherFiles_ReturnsNull(string fileName)
    {
        Assert.Null(AdrCatalog.ParseRecord(fileName, "# anything"));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("0007")]
    [InlineData("ADR-0007")]
    [InlineData("adr 7")]
    public void Search_WithNumber_ReturnsThatRecordInFull(string query)
    {
        var result = Catalog().Search(query, status: null);

        Assert.True(result.MatchedByNumber);
        Assert.Equal(7, Assert.Single(result.Records).Number);
    }

    [Fact]
    public void Search_WithWords_RanksTitleMatchesFirst()
    {
        var result = Catalog().Search("outbox events", status: null);

        Assert.False(result.MatchedByNumber);
        Assert.Equal(7, result.Records[0].Number);
    }

    [Fact]
    public void Search_WithStatus_FiltersRecords()
    {
        var result = Catalog().Search("use", status: "Proposed");

        Assert.Equal(9, Assert.Single(result.Records).Number);
    }

    [Fact]
    public void Search_WithNoMatch_ReturnsNothing()
    {
        Assert.Empty(Catalog().Search("kubernetes", status: null).Records);
    }

    [Fact]
    public async Task ReadAsync_ListsDecisionsOfMatchingRecords()
    {
        var repo = new InMemoryRepo(new()
        {
            ["docs/adr/0000-template.md"] = "# template",
            ["docs/adr/0007-use-outbox.md"] = Adr(7, "Use the outbox pattern", "Accepted", "Events go to an outbox."),
        });

        var text = await new SearchAdrsTool(repo).ReadAsync(
            Build.Caller("developers"), new() { ["query"] = "outbox" }, CancellationToken.None);

        Assert.Contains("### ADR 0007: Use the outbox pattern (Accepted)", text);
        Assert.Contains("Events go to an outbox.", text);
    }
}
