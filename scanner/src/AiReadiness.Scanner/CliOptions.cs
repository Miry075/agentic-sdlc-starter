using AiReadiness.Scanner.Reporting;

namespace AiReadiness.Scanner;

public sealed record CliOptions(
    string Path,
    bool Json,
    bool All,
    int? MinScore,
    string? HtmlPath,
    CallToAction? Cta,
    bool Fix = false,
    bool Apply = false,
    bool NoOpen = false,
    bool Html = false)
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        var path = ".";
        bool json = false, all = false;
        int? minScore = null;
        string? html = null, ctaUrl = null, ctaText = null;
        options = new CliOptions(path, false, false, null, null, null);
        bool fix = false, apply = false, noOpen = false, htmlFlag = false;
        error = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--all":
                    all = true;
                    break;
                case "--html":
                    htmlFlag = true;
                    break;
                case "--no-open":
                    noOpen = true;
                    break;
                case "--fix":
                    fix = true;
                    break;
                case "--apply":
                    apply = true;
                    break;
                case "--min-score":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out var min) || min is < 0 or > 100)
                    {
                        error = "--min-score needs a number from 0 to 100.";
                        return false;
                    }

                    minScore = min;
                    break;
                case "--out" or "--cta-url" or "--cta-text":
                    var option = args[i];
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"{option} needs a value.";
                        return false;
                    }

                    var value = args[++i];
                    if (option == "--out") html = value;
                    else if (option == "--cta-url") ctaUrl = value;
                    else ctaText = value;
                    break;
                case var unknown when unknown.StartsWith("--", StringComparison.Ordinal):
                    error = $"Unknown option {unknown}.";
                    return false;
                default:
                    path = args[i];
                    break;
            }
        }

        CallToAction? cta = null;
        if (ctaUrl is not null)
        {
            if (!Uri.TryCreate(ctaUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            {
                error = "--cta-url must be an http or https address.";
                return false;
            }

            cta = new CallToAction(ctaText ?? "Talk about your results", url);
        }
        else if (ctaText is not null)
        {
            error = "--cta-text needs --cta-url.";
            return false;
        }

        if (apply && !fix)
        {
            error = "--apply only works together with --fix.";
            return false;
        }

        if (fix && (all || json || htmlFlag || html is not null))
        {
            error = "--fix works on one repository and cannot be combined with --all, --json, --html or --out.";
            return false;
        }

        options = new CliOptions(path, json, all, minScore, html, cta, fix, apply, noOpen, htmlFlag);
        return true;
    }
}
