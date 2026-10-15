using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Knowledge.Upstreams;

/// <summary>
/// Fingerprint of a tool definition (name, description, input schema). If an upstream changes what a tool
/// says about itself, the pin changes and the tool is disabled until an administrator approves it again.
/// This is the defence against a tool whose description is rewritten to instruct the model.
/// </summary>
public static class PinCalculator
{
    public static string Compute(UpstreamToolDefinition tool)
    {
        var canonical = new StringBuilder();
        Write(
            new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description ?? string.Empty,
                ["inputSchema"] = tool.InputSchema.DeepClone(),
            },
            canonical);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Canonical form: object keys sorted ordinally, no whitespace, so formatting and key order never change a pin.
    private static void Write(JsonNode? node, StringBuilder text)
    {
        switch (node)
        {
            case null:
                text.Append("null");
                break;

            case JsonObject obj:
                text.Append('{');
                var first = true;
                foreach (var (key, value) in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (!first)
                    {
                        text.Append(',');
                    }

                    first = false;
                    text.Append(JsonSerializer.Serialize(key)).Append(':');
                    Write(value, text);
                }

                text.Append('}');
                break;

            case JsonArray array:
                text.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(',');
                    }

                    Write(array[i], text);
                }

                text.Append(']');
                break;

            default:
                text.Append(node.ToJsonString());
                break;
        }
    }
}
