using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Knowledge.Core;

namespace Knowledge.Adapters.AzureDevOps;

/// <summary>Maps Azure DevOps REST responses to the gateway's work item. Pure: no network.</summary>
public static partial class WorkItemMapper
{
    [GeneratedRegex(@"/workitems/(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex WorkItemUrl();

    public static WorkItem Map(JsonElement workItem, JsonElement? comments)
    {
        var fields = workItem.GetProperty("fields");

        return new WorkItem(
            workItem.GetProperty("id").GetInt32(),
            Text(fields, "System.Title"),
            Text(fields, "System.State"),
            NullIfEmpty(HtmlText.ToPlain(Text(fields, "System.Description"))),
            NullIfEmpty(HtmlText.ToPlain(Text(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"))),
            MapComments(comments),
            MapLinks(workItem));
    }

    private static IReadOnlyList<WorkItemComment> MapComments(JsonElement? comments)
    {
        if (comments is not { } root || !root.TryGetProperty("comments", out var list))
        {
            return [];
        }

        var result = new List<WorkItemComment>();
        foreach (var comment in list.EnumerateArray())
        {
            var text = HtmlText.ToPlain(Text(comment, "text"));
            if (text.Length == 0)
            {
                continue;
            }

            var created = comment.TryGetProperty("createdDate", out var date)
                && DateTimeOffset.TryParse(date.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                    ? parsed
                    : DateTimeOffset.MinValue;

            result.Add(new WorkItemComment(created, text));
        }

        return result;
    }

    private static IReadOnlyList<string> MapLinks(JsonElement workItem)
    {
        if (!workItem.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var links = new List<string>();
        foreach (var relation in relations.EnumerateArray())
        {
            var url = Text(relation, "url");
            var match = WorkItemUrl().Match(url);
            if (!match.Success)
            {
                continue; // attachments, hyperlinks, commits: not exposed
            }

            var name = relation.TryGetProperty("attributes", out var attributes)
                ? Text(attributes, "name")
                : string.Empty;

            links.Add($"{(name.Length > 0 ? name : Text(relation, "rel"))} #{match.Groups[1].Value}");
        }

        return links;
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
