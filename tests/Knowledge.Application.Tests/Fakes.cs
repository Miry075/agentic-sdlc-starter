using System.Text.Json.Nodes;
using Knowledge.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knowledge.Application.Tests;

internal sealed class InMemoryAuditLog : IAuditLog
{
    public List<AuditEvent> Events { get; } = [];

    public void Write(AuditEvent auditEvent) => Events.Add(auditEvent);
}

internal sealed class AllPinsApproved : IToolPinStatus
{
    public bool IsApproved(string qualifiedToolName) => true;
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class InMemoryRepo(Dictionary<string, string> files) : IRepoContextSource
{
    public Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken) =>
        Task.FromResult(files.TryGetValue(relativePath, out var text) ? text : null);

    public Task<IReadOnlyList<string>> ListFilesAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        var prefix = relativeDirectory.TrimEnd('/') + "/";
        IReadOnlyList<string> found = files.Keys
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal) && !path[prefix.Length..].Contains('/'))
            .Order()
            .ToList();
        return Task.FromResult(found);
    }
}

internal sealed class FakeWorkItems : IWorkItemSource
{
    private readonly Dictionary<int, WorkItem> _items = [];

    public int Calls { get; private set; }

    public bool Throws { get; set; }

    public FakeWorkItems With(WorkItem item)
    {
        _items[item.Id] = item;
        return this;
    }

    public Task<WorkItem?> GetAsync(CallerContext caller, int id, CancellationToken cancellationToken)
    {
        Calls++;
        if (Throws)
        {
            throw new SourceUnavailableException("work items");
        }

        return Task.FromResult(_items.GetValueOrDefault(id));
    }
}

/// <summary>A tool whose behaviour the test scripts. Records what it was asked to read.</summary>
internal sealed class ScriptedTool(string name, string layer, Func<JsonObject, Task<string>> read) : IGatewayTool
{
    public ToolDescriptor Descriptor { get; } = new(
        name, "scripted", layer, new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() });

    public List<JsonObject> Reads { get; } = [];

    public Task<string> ReadAsync(CallerContext caller, JsonObject arguments, CancellationToken cancellationToken)
    {
        Reads.Add(arguments);
        return read(arguments);
    }
}

internal sealed class FixedToolSource(params IGatewayTool[] tools) : IToolSource
{
    public IReadOnlyCollection<IGatewayTool> GetTools() => tools;
}

internal static class Build
{
    public static CallerContext Caller(params string[] roles) =>
        new("user-1", roles.ToHashSet(StringComparer.Ordinal), "Test");

    public static WorkItem AWorkItem(int id, string state = "Active", string title = "Issue credit note") =>
        new(id, title, state, "Customers need to cancel an invoice.", "1. Given an issued invoice, when ...", [], []);

    public static readonly string PolicyYaml = """
        defaultDecision: deny
        tools:
          get_glossary_term: { layer: L1 }
          search_adrs:       { layer: L1 }
          get_spec:          { layer: L1, needsWorkItemState: true }
          get_work_item:     { layer: L4, needsWorkItemState: true }
          echo:              { layer: L1 }
        phases:
          requirements: { tools: [get_glossary_term, get_work_item, echo] }
          build:        { tools: [get_glossary_term, search_adrs, get_spec, echo] }
        workItemStates:
          New:    [requirements]
          Active: [build]
        roles:
          developers: "*"
          product-owners: [get_glossary_term, get_work_item]
        """;

    public static Knowledge.Policy.PhasePolicy Policy() =>
        new(Knowledge.Policy.PolicyLoader.Parse(PolicyYaml), new AllPinsApproved());

    public static ToolGuard Guard(InMemoryAuditLog audit, int maxChars = 64 * 1024) =>
        new(
            Policy(),
            Knowledge.Redaction.PatternRedactor.Create(),
            audit,
            TimeProvider.System,
            new ToolGuardOptions { MaxResultChars = maxChars },
            NullLogger<ToolGuard>.Instance);

    public static ToolDispatcher Dispatcher(InMemoryAuditLog audit, params IGatewayTool[] tools) =>
        new(
            [new FixedToolSource(tools)],
            Policy(),
            Guard(audit),
            NullLogger<ToolDispatcher>.Instance);
}
