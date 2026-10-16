using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Knowledge.Core;
using Knowledge.Policy;
using Microsoft.Extensions.Logging;

namespace Knowledge.Upstreams;

/// <summary>An approved upstream tool, offered as "&lt;upstream&gt;.&lt;tool&gt;". Reads are guarded like any other tool.</summary>
public sealed class UpstreamTool(UpstreamConnection connection, UpstreamToolDefinition definition, ILogger logger) : IGatewayTool
{
    public ToolDescriptor Descriptor { get; } = new(
        $"{connection.Name}.{definition.Name}",
        $"[via {connection.Name}] {definition.Description}".TrimEnd(),
        connection.Layer,
        (JsonObject)definition.InputSchema.DeepClone(),
        connection.Name);

    public async Task<string> ReadAsync(CallerContext caller, JsonObject arguments, CancellationToken cancellationToken)
    {
        var result = await connection.CallToolAsync(definition.Name, arguments, cancellationToken);

        var text = new StringBuilder();
        var dropped = 0;

        foreach (var content in result.Content)
        {
            if (content is UpstreamTextContent t)
            {
                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.Append(t.Text);
            }
            else
            {
                dropped++;
            }
        }

        if (dropped > 0)
        {
            logger.LogInformation("Dropped {Count} non-text items from {Tool}", dropped, Descriptor.Name);
        }

        // An upstream error is still upstream content: it is returned as text so that it is redacted like any result.
        return result.IsError ? "Upstream error: " + text : text.ToString();
    }
}

/// <summary>Reads one resource from an upstream. Only URIs matching the policy's allow list can be read.</summary>
public sealed class UpstreamResourceTool : IGatewayTool
{
    private readonly UpstreamConnection _connection;
    private readonly IReadOnlyList<Regex> _allowed;

    public UpstreamResourceTool(UpstreamConnection connection)
    {
        _connection = connection;
        _allowed = connection.Policy.ResourceAllow.Select(ToRegex).ToList();

        Descriptor = new ToolDescriptor(
            $"{connection.Name}.{PolicyLoader.ReadResourceTool}",
            $"[via {connection.Name}] Read a resource by its URI. Allowed URI patterns: {string.Join(", ", connection.Policy.ResourceAllow)}",
            connection.Layer,
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["uri"] = new JsonObject { ["type"] = "string", ["description"] = "The resource URI." },
                },
                ["required"] = new JsonArray("uri"),
            },
            connection.Name);
    }

    public ToolDescriptor Descriptor { get; }

    public async Task<string> ReadAsync(CallerContext caller, JsonObject arguments, CancellationToken cancellationToken)
    {
        var uri = arguments["uri"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;
        if (uri.Length == 0)
        {
            throw new ToolArgumentException("Argument 'uri' is required.");
        }

        if (!_allowed.Any(pattern => pattern.IsMatch(uri)))
        {
            throw new ToolArgumentException("This resource URI is not allowed by the policy.");
        }

        return await _connection.ReadResourceAsync(uri, cancellationToken) ?? "Resource not found, or not text.";
    }

    // "graphify://node/*" -> a pattern where * matches anything except nothing else is special.
    private static Regex ToRegex(string glob) =>
        new("^" + Regex.Escape(glob).Replace(@"\*", ".*") + "$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
