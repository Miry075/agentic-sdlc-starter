using System.Text.Json.Nodes;
using Knowledge.Application.RepoContext;
using Knowledge.Application.Tools;
using Knowledge.Core;

namespace Knowledge.Application.Tests;

public class GlossaryTests
{
    private const string Markdown = """
        # Domain glossary

        | Term | Definition | In code | Not to be confused with |
        |---|---|---|---|
        | **Invoice** | A legal document requesting payment. | `Invoice` | *Quote*: a proposal |
        | **Credit note** | Cancels all or part of an invoice. | `CreditNote` | *Refund* |
        | **VAT** | Value-added tax. | `VatRate` | — |

        ## Acronyms

        | Acronym | Meaning |
        |---|---|
        | AC | Acceptance criteria |
        | ADR | Architecture decision record |
        """;

    [Fact]
    public void Find_WithExactTermIgnoringCase_ReturnsTheEntry()
    {
        var lookup = Glossary.Parse(Markdown).Find("credit NOTE");

        var entry = Assert.Single(lookup.Matches);
        Assert.Equal("Credit note", entry.Term);
        Assert.Equal("CreditNote", entry.InCode);
        Assert.Equal("*Refund*", entry.NotToBeConfusedWith);
    }

    [Fact]
    public void Find_WithEmDashInConfusionColumn_TreatsItAsEmpty()
    {
        var entry = Assert.Single(Glossary.Parse(Markdown).Find("VAT").Matches);

        Assert.Null(entry.NotToBeConfusedWith);
    }

    [Fact]
    public void Find_WithAcronym_ReturnsItsMeaning()
    {
        var entry = Assert.Single(Glossary.Parse(Markdown).Find("AC").Matches);

        Assert.Equal("Acceptance criteria", entry.Definition);
        Assert.Null(entry.InCode);
    }

    [Fact]
    public void Find_WithPartOfATerm_ReturnsPartialMatches()
    {
        var lookup = Glossary.Parse(Markdown).Find("credit");

        Assert.Equal("Credit note", Assert.Single(lookup.Matches).Term);
    }

    [Fact]
    public void Find_WithUnknownTerm_SuggestsTheThreeClosest()
    {
        var lookup = Glossary.Parse(Markdown).Find("invoyce");

        Assert.Empty(lookup.Matches);
        Assert.Equal(3, lookup.Suggestions.Count);
        Assert.Equal("Invoice", lookup.Suggestions[0]);
    }

    [Fact]
    public void Parse_IgnoresHeaderAndSeparatorRows()
    {
        var lookup = Glossary.Parse(Markdown).Find("Term");

        Assert.Empty(lookup.Matches);
    }

    [Fact]
    public async Task ReadAsync_WhenGlossaryFileIsMissing_SaysSo()
    {
        var tool = new GetGlossaryTermTool(new InMemoryRepo(new Dictionary<string, string>()));

        var text = await tool.ReadAsync(Build.Caller("developers"), new() { ["term"] = "Invoice" }, CancellationToken.None);

        Assert.Contains("No glossary found", text);
    }

    [Fact]
    public async Task ReadAsync_WithoutTerm_ThrowsArgumentException()
    {
        var tool = new GetGlossaryTermTool(new InMemoryRepo(new() { ["docs/domain/glossary.md"] = Markdown }));

        await Assert.ThrowsAsync<ToolArgumentException>(
            () => tool.ReadAsync(Build.Caller("developers"), new JsonObject(), CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_WithKnownTerm_RendersDefinitionAndCodeName()
    {
        var tool = new GetGlossaryTermTool(new InMemoryRepo(new() { ["docs/domain/glossary.md"] = Markdown }));

        var text = await tool.ReadAsync(Build.Caller("developers"), new() { ["term"] = "Invoice" }, CancellationToken.None);

        Assert.Equal(
            "**Invoice**: A legal document requesting payment.\n- In code: `Invoice`\n- Not to be confused with: *Quote*: a proposal"
                .Replace("\n", Environment.NewLine),
            text);
    }
}
