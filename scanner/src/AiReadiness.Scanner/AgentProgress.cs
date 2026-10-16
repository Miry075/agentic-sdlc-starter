using System.Text.Json;

namespace AiReadiness.Scanner;

/// <summary>
/// Turns Claude Code's stream-json events into one readable log line per step:
/// what it says, which file it reads or writes, which command it runs, what was refused, and the final result.
/// </summary>
public static class AgentProgress
{
    private const int MaxDetail = 160;

    /// <returns>The lines to show for this event, or none for events that carry nothing useful.</returns>
    public static IReadOnlyList<string> Describe(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            return Str(root, "type") switch
            {
                "system" when Str(root, "subtype") == "init" => [$"Claude Code started ({Str(root, "model") ?? "default model"})"],
                "assistant" => Blocks(root).SelectMany(Assistant).ToList(),
                "user" => Blocks(root).SelectMany(ToolResult).ToList(),
                "result" => Result(root),
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [Short(line)];
        }
    }

    private static IEnumerable<JsonElement> Blocks(JsonElement root) =>
        root.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray().ToList()
            : [];

    private static IEnumerable<string> Assistant(JsonElement block)
    {
        switch (Str(block, "type"))
        {
            case "text" when Str(block, "text") is { Length: > 0 } text:
                foreach (var l in text.Trim().Split('\n').Where(l => l.Trim().Length > 0))
                {
                    yield return "  " + l.TrimEnd();
                }

                break;
            case "tool_use":
                yield return Tool(Str(block, "name") ?? "tool", block.TryGetProperty("input", out var input) ? input : default);
                break;
        }
    }

    private static string Tool(string name, JsonElement input)
    {
        string? Field(string key) => input.ValueKind == JsonValueKind.Object ? Str(input, key) : null;

        return name switch
        {
            "Bash" => "$ " + Short(FirstLine(Field("command") ?? "")),
            "Read" => "read   " + Field("file_path"),
            "Write" => "write  " + Field("file_path"),
            "Edit" or "MultiEdit" => "edit   " + Field("file_path"),
            "Glob" => "find   " + Field("pattern"),
            "Grep" => "search " + Short(Field("pattern") ?? ""),
            "Skill" => "skill  " + Field("skill"),
            "TodoWrite" or "TaskCreate" or "TaskUpdate" => "plan   updated",
            _ => "tool   " + name,
        };
    }

    private static IEnumerable<string> ToolResult(JsonElement block)
    {
        if (Str(block, "type") == "tool_result" && block.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
        {
            yield return "  ! " + Short(FirstLine(ResultText(block)));
        }
    }

    private static string ResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content))
        {
            return "error";
        }

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? "error",
            JsonValueKind.Array => content.EnumerateArray().Select(c => Str(c, "text")).FirstOrDefault(t => t is not null) ?? "error",
            _ => "error",
        };
    }

    private static IReadOnlyList<string> Result(JsonElement root)
    {
        var lines = new List<string>();
        var seconds = root.TryGetProperty("duration_ms", out var ms) && ms.TryGetDouble(out var d) ? $"{d / 1000:0}s" : "?";
        var turns = root.TryGetProperty("num_turns", out var n) && n.TryGetInt32(out var t) ? t.ToString() : "?";
        var failed = root.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True;
        lines.Add(failed ? $"Claude Code stopped with an error after {seconds}, {turns} turns" : $"Claude Code finished in {seconds}, {turns} turns");
        if (Str(root, "result") is { Length: > 0 } text)
        {
            lines.Add("");
            lines.AddRange(text.Trim().Split('\n').Select(l => l.TrimEnd()));
        }

        return lines;
    }

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string FirstLine(string text) => text.Split('\n')[0];

    private static string Short(string text) => text.Length <= MaxDetail ? text : text[..MaxDetail] + "…";
}
