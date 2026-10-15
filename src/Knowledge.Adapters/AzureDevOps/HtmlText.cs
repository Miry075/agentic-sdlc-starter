using System.Net;
using System.Text.RegularExpressions;

namespace Knowledge.Adapters.AzureDevOps;

/// <summary>
/// Turns the HTML that Azure DevOps stores in rich-text fields into plain text.
/// Hidden content (comments, scripts, styles) is dropped so that nothing invisible in the tracker reaches the model.
/// </summary>
public static partial class HtmlText
{
    [GeneratedRegex(@"<!--.*?-->|<(script|style)\b.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HiddenContent();

    [GeneratedRegex(@"<\s*(?:br\s*/?|/p|/div|/h[1-6]|/tr)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"<\s*li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItems();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex TrailingSpaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    public static string ToPlain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = HiddenContent().Replace(html, string.Empty);
        text = LineBreaks().Replace(text, "\n");
        text = ListItems().Replace(text, "\n- ");
        text = Tags().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace(" ", " ").Replace("\r", string.Empty);
        text = TrailingSpaces().Replace(text, "\n");
        text = BlankLines().Replace(text, "\n\n");
        return text.Trim();
    }
}
