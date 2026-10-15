using System.Text.Json.Nodes;
using Knowledge.Application;
using Knowledge.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Knowledge.Gateway.IntegrationTests;

/// <summary>
/// The whole gateway, wired as it runs, in dev mode against samples/demo-repo.
/// These tests call the dispatcher directly; they do not speak the MCP wire protocol.
/// </summary>
public class DevModeEndToEndTests(GatewayFactory factory) : IClassFixture<GatewayFactory>
{
    private static readonly CallerContext Developer = new("dev@localhost", new HashSet<string> { "developers" }, "Dev");

    private ToolDispatcher Dispatcher => factory.Services.GetRequiredService<ToolDispatcher>();

    private static JsonObject Args(string phase, string key, JsonNode value) => new() { ["phase"] = phase, [key] = value };

    [Fact]
    public void ListTools_ForDeveloper_OffersTheFourNativeTools()
    {
        var names = Dispatcher.ListTools(Developer).Select(t => t.Name).ToArray();

        Assert.Equal(["get_glossary_term", "get_spec", "get_work_item", "search_adrs"], names);
    }

    [Fact]
    public void ListTools_ForCallerWithoutAMappedRole_OffersNothing()
    {
        var stranger = new CallerContext("someone", new HashSet<string>(), "Dev");

        Assert.Empty(Dispatcher.ListTools(stranger));
    }

    [Fact]
    public async Task GetGlossaryTerm_InRequirements_ReturnsTheDefinition()
    {
        var result = await Dispatcher.CallAsync(
            Developer, "get_glossary_term", Args("requirements", "term", "credit note"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("cancels all or part of an issued invoice", result.Text);
    }

    [Fact]
    public async Task SearchAdrs_InBuild_FindsTheOutboxDecision()
    {
        var result = await Dispatcher.CallAsync(
            Developer, "search_adrs", Args("build", "query", "outbox"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("ADR 0002", result.Text);
    }

    [Fact]
    public async Task GetSpec_ForAnActiveWorkItemInBuild_ReturnsTheSpec()
    {
        var result = await Dispatcher.CallAsync(
            Developer, "get_spec", Args("build", "workItemId", 42), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("Issue a credit note for an invoice", result.Text);
    }

    [Fact]
    public async Task GetSpec_ForANewWorkItemInBuild_IsDeniedByTheWorkItemState()
    {
        var result = await Dispatcher.CallAsync(
            Developer, "get_spec", Args("build", "workItemId", 41), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Work item in state 'New' does not allow phase 'build'.", result.Text);
    }

    [Fact]
    public async Task GetWorkItem_InRequirements_ReturnsItWithPersonalDataRedacted()
    {
        var result = await Dispatcher.CallAsync(
            Developer, "get_work_item", Args("requirements", "id", 41), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("Clarify VAT rules for credit notes", result.Text);
        Assert.Contains("[EMAIL]", result.Text);
        Assert.DoesNotContain("finance.lead@example.test", result.Text);
    }

    [Fact]
    public async Task GetWorkItem_InBuild_IsNotAvailableInThatPhase()
    {
        var result = await Dispatcher.CallAsync(
            Developer, "get_work_item", Args("build", "id", 42), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("'get_work_item' is not available in phase 'build'.", result.Text);
    }

    [Fact]
    public async Task GetWorkItem_ForAClosedWorkItem_IsDeniedInEveryPhase()
    {
        var result = await Dispatcher.CallAsync(
            Developer, "get_work_item", Args("release", "id", 43), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Work item in state 'Closed' does not allow phase 'release'.", result.Text);
    }

    [Fact]
    public async Task EveryCall_WritesExactlyOneAuditEvent()
    {
        var before = factory.Audit.Events.Count;

        await Dispatcher.CallAsync(Developer, "get_glossary_term", Args("requirements", "term", "invoice"), CancellationToken.None);
        await Dispatcher.CallAsync(Developer, "search_adrs", Args("requirements", "query", "outbox"), CancellationToken.None);

        var events = factory.Audit.Events.Skip(before).ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal(AuditDecision.Allow, events[0].Decision);
        Assert.Equal(AuditDecision.Deny, events[1].Decision);
        Assert.All(events, e => Assert.Equal("Dev", e.AuthMode));
    }

    [Fact]
    public async Task HealthLive_Responds()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task HealthReady_WhenTheRepoSourceAnswers_Responds()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public void Startup_WithDevAuthInProduction_IsRefused()
    {
        using var production = new ProductionGatewayFactory();

        var failure = Record.Exception(() => production.CreateClient());

        Assert.NotNull(failure);
        Assert.Contains("not allowed", Messages(failure));
    }

    private static string Messages(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            messages.Add(current.Message);
            if (current.InnerException is null)
            {
                break;
            }
        }

        return string.Join(" | ", messages);
    }
}
