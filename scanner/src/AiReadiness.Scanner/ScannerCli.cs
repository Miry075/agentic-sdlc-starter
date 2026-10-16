using AiReadiness.Scanner.Checks;
using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Reporting;

namespace AiReadiness.Scanner;

public static class ScannerCli
{
    public const int Ok = 0;
    public const int BelowMinimum = 1;
    public const int UsageError = 2;

    private const string Usage = """
        Usage: ai-readiness [path] [--json] [--min-score <0-100>]

          path          repository folder, relative or absolute (default: current folder)
          --json        print the full report as JSON instead of markdown
          --min-score   exit with code 1 when the score is lower (for CI)
        """;

    public static IReadOnlyList<IReadinessCheck> DefaultChecks { get; } =
    [
        new AgentContextCheck(),
        new GlossaryCheck(),
        new AdrCheck(),
        new SpecCheck(),
        new CiFreshnessCheck(),
        new SensitiveDataCheck(),
        new EvaluationSetCheck(),
        new AgentSettingsCheck(),
    ];

    public static int Run(string[] args, TextWriter output, TextWriter error, Func<string, IRepoFiles>? open = null)
    {
        open ??= path => new DiskRepoFiles(path);
        string path = ".";
        var json = false;
        int? minScore = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--min-score":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out var min) || min is < 0 or > 100)
                    {
                        error.WriteLine("--min-score needs a number from 0 to 100.");
                        return UsageError;
                    }

                    minScore = min;
                    break;
                case "-h" or "--help":
                    output.WriteLine(Usage);
                    return Ok;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    error.WriteLine($"Unknown option {option}.{Environment.NewLine}{Usage}");
                    return UsageError;
                default:
                    path = args[i];
                    break;
            }
        }

        IRepoFiles repo;
        try
        {
            repo = open(path);
        }
        catch (DirectoryNotFoundException ex)
        {
            error.WriteLine(ex.Message);
            return UsageError;
        }

        var assessment = new ReadinessScanner(DefaultChecks).Scan(repo);
        var name = new DirectoryInfo(Path.GetFullPath(path)).Name;
        output.WriteLine(json ? JsonReport.Render(assessment, name) : MarkdownReport.Render(assessment, name));

        return minScore is { } required && assessment.Score < required ? BelowMinimum : Ok;
    }
}
