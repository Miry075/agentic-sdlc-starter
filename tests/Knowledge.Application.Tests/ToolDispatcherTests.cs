using System.Text.Json.Nodes;
using Knowledge.Application.Tools;
using Knowledge.Application.WorkItems;
using Knowledge.Core;

namespace Knowledge.Application.Tests;

public class ToolDispatcherTests
{
    private static readonly CallerContext Developer = Build.Caller("developers");

    private static ScriptedTool Echo() => new("echo", "L1", _ => Task.FromResult("echoed"));

    private static JsonObject Args(string? phase, params (string Key, JsonNode Value)[] more)
    {
        var args = new JsonObject();
        if (phase is not null)
        {
            args["phase"] = phase;
        }

        foreach (var (key, value) in more)
        {
            args[key] = value;
        }

        return args;
    }

    [Fact]
    public void ListTools_ForDeveloper_ReturnsToolsWithRequiredPhaseArgument()
    {
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), Echo());

        var tool = Assert.Single(dispatcher.ListTools(Developer));

        Assert.Equal("echo", tool.Name);
        var phase = tool.InputSchema["properties"]!["phase"]!;
        Assert.Equal("string", phase["type"]!.GetValue<string>());
        Assert.Equal(["build", "requirements"], phase["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        Assert.Contains("phase", tool.InputSchema["required"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void ListTools_DoesNotChangeTheToolsOwnSchema()
    {
        var echo = Echo();
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), echo);

        dispatcher.ListTools(Developer);

        Assert.Null(echo.Descriptor.InputSchema["properties"]!["phase"]);
    }

    [Fact]
    public void ListTools_ForCallerWithoutMappedRole_ReturnsNothing()
    {
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), Echo());

        Assert.Empty(dispatcher.ListTools(Build.Caller("some-other-group")));
    }

    [Fact]
    public async Task CallAsync_RemovesPhaseBeforeTheToolReadsItsArguments()
    {
        var echo = Echo();
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), echo);

        var result = await dispatcher.CallAsync(Developer, "echo", Args("build", ("q", "x")), CancellationToken.None);

        Assert.False(result.IsError);
        var seen = Assert.Single(echo.Reads);
        Assert.Null(seen["phase"]);
        Assert.Equal("x", seen["q"]!.GetValue<string>());
    }

    [Fact]
    public async Task CallAsync_WhenPhaseIsMissing_DeniesAndAudits()
    {
        var audit = new InMemoryAuditLog();
        var dispatcher = Build.Dispatcher(audit, Echo());

        var result = await dispatcher.CallAsync(Developer, "echo", Args(null), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("The 'phase' argument is required.", result.Text);
        Assert.Equal(AuditDecision.Deny, Assert.Single(audit.Events).Decision);
    }

    [Fact]
    public async Task CallAsync_WhenToolIsHiddenFromTheCaller_StillDeniesTheCallByName()
    {
        var audit = new InMemoryAuditLog();
        var dispatcher = Build.Dispatcher(audit, Echo());

        var result = await dispatcher.CallAsync(Build.Caller("product-owners"), "echo", Args("build"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Your role does not allow 'echo'.", result.Text);
    }

    [Fact]
    public async Task CallAsync_WhenToolDoesNotExist_DeniesAndAudits()
    {
        var audit = new InMemoryAuditLog();
        var dispatcher = Build.Dispatcher(audit, Echo());

        var result = await dispatcher.CallAsync(Developer, "drop_database", Args("build"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Unknown tool 'drop_database'.", result.Text);
        Assert.Single(audit.Events);
    }

    [Fact]
    public async Task CallAsync_ForSpecWhenWorkItemStateAllowsPhase_ReturnsTheSpec()
    {
        var repo = new InMemoryRepo(new() { ["docs/specs/42.md"] = "# 42: Credit notes" });
        var workItems = new FakeWorkItems().With(Build.AWorkItem(42, state: "Active"));
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), new GetSpecTool(repo, workItems));

        var result = await dispatcher.CallAsync(
            Developer, "get_spec", Args("build", ("workItemId", 42)), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("# 42: Credit notes", result.Text);
    }

    [Fact]
    public async Task CallAsync_ForSpecWhenWorkItemStateDoesNotAllowPhase_Denies()
    {
        var repo = new InMemoryRepo(new() { ["docs/specs/42.md"] = "# 42: Credit notes" });
        var workItems = new FakeWorkItems().With(Build.AWorkItem(42, state: "New"));
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), new GetSpecTool(repo, workItems));

        var result = await dispatcher.CallAsync(
            Developer, "get_spec", Args("build", ("workItemId", 42)), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Work item in state 'New' does not allow phase 'build'.", result.Text);
    }

    [Fact]
    public async Task CallAsync_WhenWorkItemCannotBeRead_DeniesBecauseTheStateIsUnknown()
    {
        var repo = new InMemoryRepo(new() { ["docs/specs/42.md"] = "# 42" });
        var workItems = new FakeWorkItems { Throws = true };
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), new GetSpecTool(repo, workItems));

        var result = await dispatcher.CallAsync(
            Developer, "get_spec", Args("build", ("workItemId", 42)), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("The work item state could not be determined.", result.Text);
    }

    [Fact]
    public async Task CallAsync_ForWorkItem_ReturnsOnlyAllowlistedFields()
    {
        var workItems = new FakeWorkItems().With(Build.AWorkItem(7, state: "New", title: "Issue credit note"));
        var formatter = new WorkItemFormatter(new HashSet<string> { "id", "title", "state" });
        var dispatcher = Build.Dispatcher(new InMemoryAuditLog(), new GetWorkItemTool(workItems, formatter));

        var result = await dispatcher.CallAsync(
            Developer, "get_work_item", Args("requirements", ("id", 7)), CancellationToken.None);

        Assert.Contains("#7 Issue credit note", result.Text);
        Assert.Contains("New", result.Text);
        Assert.DoesNotContain("Customers need to cancel", result.Text);
    }
}
