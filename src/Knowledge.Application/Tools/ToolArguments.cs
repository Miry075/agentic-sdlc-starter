using System.Text.Json.Nodes;
using Knowledge.Core;

namespace Knowledge.Application.Tools;

internal static class ToolArguments
{
    public const int MaxTextLength = 200;

    public static string RequireText(JsonObject arguments, string name)
    {
        var value = OptionalText(arguments, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new ToolArgumentException($"Argument '{name}' is required.")
            : value;
    }

    public static string? OptionalText(JsonObject arguments, string name)
    {
        if (arguments[name] is not JsonValue node)
        {
            return null;
        }

        if (!node.TryGetValue<string>(out var text))
        {
            throw new ToolArgumentException($"Argument '{name}' must be text.");
        }

        text = text.Trim();
        return text.Length > MaxTextLength
            ? throw new ToolArgumentException($"Argument '{name}' is longer than {MaxTextLength} characters.")
            : text;
    }

    /// <summary>A work item id, given as a number or as digits in text. Never used as a path unless it is only digits.</summary>
    public static int RequireWorkItemId(JsonObject arguments, string name)
    {
        if (arguments[name] is JsonValue node)
        {
            if (node.TryGetValue<int>(out var number) && number > 0)
            {
                return number;
            }

            if (node.TryGetValue<string>(out var text)
                && text.Length is > 0 and <= 9
                && text.All(char.IsAsciiDigit)
                && int.Parse(text, System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new ToolArgumentException($"Argument '{name}' must be a positive work item number.");
    }
}

internal static class SchemaBuilder
{
    public static JsonObject Object(params (string Name, string Type, string Description, bool Required)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();

        foreach (var (name, type, description, isRequired) in properties)
        {
            props[name] = new JsonObject { ["type"] = type, ["description"] = description };
            if (isRequired)
            {
                required.Add(name);
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = required,
        };
    }
}
