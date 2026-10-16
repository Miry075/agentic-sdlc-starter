using AiReadiness.Scanner.Reporting;

namespace AiReadiness.Scanner;

public sealed record CliOptions(
    string Path,
    bool Json,
    bool All,
    int? MinScore,
    string? HtmlPath,
    CallToAction? Cta)
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        var path = ".";
        bool json = false, all = false;
        int? minScore = null;
        string? html = null, ctaUrl = null, ctaText = null;
        options = new CliOptions(path, false, false, null, null, null);
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
                case "--min-score":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out var min) || min is < 0 or > 100)
                    {
                        error = "--min-score needs a number from 0 to 100.";
                        return false;
                    }

                    minScore = min;
                    break;
                case "--html" or "--cta-url" or "--cta-text":
                    var option = args[i];
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"{option} needs a value.";
                        return false;
                    }

                    var value = args[++i];
                    if (option == "--html") html = value;
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

        options = new CliOptions(path, json, all, minScore, html, cta);
        return true;
    }
}
