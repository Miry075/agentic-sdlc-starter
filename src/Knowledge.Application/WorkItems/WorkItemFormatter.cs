using System.Text;
using Knowledge.Core;

namespace Knowledge.Application.WorkItems;

/// <summary>Renders a work item as markdown, including only the fields the policy allows.</summary>
public sealed class WorkItemFormatter(IReadOnlySet<string> allowedFields)
{
    public const int MaxComments = 20;

    public string ToMarkdown(WorkItem item)
    {
        var text = new StringBuilder();

        if (Allows("id") || Allows("title"))
        {
            text.Append("# ");
            if (Allows("id"))
            {
                text.Append('#').Append(item.Id).Append(' ');
            }

            if (Allows("title"))
            {
                text.Append(item.Title);
            }

            text.AppendLine().AppendLine();
        }

        if (Allows("state"))
        {
            text.Append("**State:** ").AppendLine(item.State).AppendLine();
        }

        AppendSection(text, "description", "Description", item.Description);
        AppendSection(text, "acceptanceCriteria", "Acceptance criteria", item.AcceptanceCriteria);

        if (Allows("links") && item.Links.Count > 0)
        {
            text.AppendLine("## Links");
            foreach (var link in item.Links)
            {
                text.Append("- ").AppendLine(link);
            }

            text.AppendLine();
        }

        if (Allows("comments") && item.Comments.Count > 0)
        {
            text.AppendLine("## Comments (latest first)");
            foreach (var comment in item.Comments.OrderByDescending(c => c.CreatedAt).Take(MaxComments))
            {
                text.Append("- ").Append(comment.CreatedAt.ToString("yyyy-MM-dd")).Append(": ").AppendLine(comment.Text.Trim());
            }
        }

        return text.ToString().TrimEnd();
    }

    private void AppendSection(StringBuilder text, string field, string heading, string? body)
    {
        if (Allows(field) && !string.IsNullOrWhiteSpace(body))
        {
            text.Append("## ").AppendLine(heading).AppendLine(body.Trim()).AppendLine();
        }
    }

    private bool Allows(string field) => allowedFields.Contains(field);
}
