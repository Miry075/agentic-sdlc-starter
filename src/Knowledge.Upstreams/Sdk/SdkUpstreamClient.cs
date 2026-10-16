using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Knowledge.Upstreams.Sdk;

// This file and SdkUpstreamClientFactory.cs are the only code that touches the ModelContextProtocol SDK.
// The SDK is still evolving: if a build error points here after an SDK upgrade, fix it here and nowhere else.

internal sealed class SdkUpstreamClient(McpClient client) : IUpstreamClient
{
    public async Task<IReadOnlyList<UpstreamToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

        return tools
            .Select(tool => new UpstreamToolDefinition(
                tool.ProtocolTool.Name,
                tool.ProtocolTool.Description,
                ToObject(tool.ProtocolTool.InputSchema)))
            .ToList();
    }

    public async Task<UpstreamCallResult> CallToolAsync(string name, JsonObject arguments, CancellationToken cancellationToken)
    {
        var parameters = arguments.ToDictionary(
            pair => pair.Key,
            pair => (object?)JsonSerializer.Deserialize<JsonElement>(pair.Value?.ToJsonString() ?? "null"));

        var result = await client.CallToolAsync(name, parameters, cancellationToken: cancellationToken);

        var content = result.Content
            .Select(block => block is TextContentBlock text
                ? (UpstreamContent)new UpstreamTextContent(text.Text)
                : new UpstreamOtherContent(block.Type))
            .ToList();

        return new UpstreamCallResult(result.IsError ?? false, content);
    }

    public async Task<string?> ReadResourceTextAsync(string uri, CancellationToken cancellationToken)
    {
        var result = await client.ReadResourceAsync(uri, cancellationToken: cancellationToken);

        var texts = result.Contents.OfType<TextResourceContents>().Select(c => c.Text).ToList();
        return texts.Count == 0 ? null : string.Join(Environment.NewLine, texts);
    }

    public ValueTask DisposeAsync() => client.DisposeAsync();

    private static JsonObject ToObject(JsonElement schema) =>
        JsonNode.Parse(schema.GetRawText()) as JsonObject ?? new JsonObject { ["type"] = "object" };
}
