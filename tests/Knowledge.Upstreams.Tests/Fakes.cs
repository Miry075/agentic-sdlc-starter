using System.Text.Json.Nodes;
using Knowledge.Core;
using Knowledge.Policy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knowledge.Upstreams.Tests;

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal sealed class FakeUpstreamClient : IUpstreamClient
{
    public List<UpstreamToolDefinition> Tools { get; } = [];

    public Dictionary<string, string> Resources { get; } = [];

    public List<(string Name, JsonObject Arguments)> Calls { get; } = [];

    /// <summary>How many of the next operations fail with a transient I/O error.</summary>
    public int FailNext { get; set; }

    public Func<string, UpstreamCallResult> Respond { get; set; } =
        _ => new UpstreamCallResult(false, [new UpstreamTextContent("ok")]);

    public int Operations { get; private set; }

    public Task<IReadOnlyList<UpstreamToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
    {
        Fail();
        return Task.FromResult<IReadOnlyList<UpstreamToolDefinition>>(Tools.ToList());
    }

    public Task<UpstreamCallResult> CallToolAsync(string name, JsonObject arguments, CancellationToken cancellationToken)
    {
        Fail();
        Calls.Add((name, (JsonObject)arguments.DeepClone()));
        return Task.FromResult(Respond(name));
    }

    public Task<string?> ReadResourceTextAsync(string uri, CancellationToken cancellationToken)
    {
        Fail();
        return Task.FromResult(Resources.GetValueOrDefault(uri));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void Fail()
    {
        Operations++;
        if (FailNext > 0)
        {
            FailNext--;
            throw new IOException("connection reset");
        }
    }
}

internal sealed class FakeFactory(FakeUpstreamClient client) : IUpstreamClientFactory
{
    public int Connects { get; private set; }

    public Task<IUpstreamClient> ConnectAsync(UpstreamConfig config, CancellationToken cancellationToken)
    {
        Connects++;
        return Task.FromResult<IUpstreamClient>(client);
    }
}

internal static class Samples
{
    public static UpstreamToolDefinition QueryGraph(string description = "Query the code graph.") =>
        new("query_graph", description, new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["query"] = new JsonObject { ["type"] = "string" } },
            ["required"] = new JsonArray("query"),
        });

    public static UpstreamToolDefinition DeleteNode() =>
        new("delete_node", "Deletes a node.", new JsonObject { ["type"] = "object" });

    public static UpstreamConfig Config(string name = "graphify") =>
        new() { Name = name, Transport = UpstreamTransport.Stdio, Command = "python", TimeoutSeconds = 5 };

    /// <summary>Policy that approves query_graph with the given pin and offers resources under graphify://node/.</summary>
    public static PolicyDocument Policy(string? queryGraphPin, bool withResources = false)
    {
        var extraTool = queryGraphPin is null ? string.Empty : ", graphify.query_graph";
        var pinYaml = queryGraphPin is null ? "{}" : "{ pin: \"" + queryGraphPin + "\" }";
        var resources = withResources
            ? "    resources:\n      allow: [\"graphify://node/*\"]\n      phases: [design]"
            : string.Empty;

        return PolicyLoader.Parse($$"""
            defaultDecision: deny
            tools:
              get_glossary_term: { layer: L1 }
            phases:
              design: { tools: [get_glossary_term{{extraTool}}] }
              build:  { tools: [get_glossary_term{{extraTool}}] }
              test:   { tools: [get_glossary_term] }
            workItemStates:
              Active: [build]
            roles:
              developers: "*"
            upstreams:
              graphify:
                layer: L2
                tools:
                  query_graph: {{pinYaml}}
            {{resources}}
            """);
    }

    public static UpstreamConnection Connection(
        FakeUpstreamClient client, UpstreamPolicy policy, ManualClock? clock = null, UpstreamConfig? config = null) =>
        new(
            config ?? Config(),
            policy,
            new FakeFactory(client),
            clock ?? new ManualClock(DateTimeOffset.UnixEpoch),
            NullLogger<UpstreamConnection>.Instance);
}

internal sealed class InMemoryAuditLog : IAuditLog
{
    public List<AuditEvent> Events { get; } = [];

    public void Write(AuditEvent auditEvent) => Events.Add(auditEvent);
}
