using AiReadiness.Scanner.Checks;
using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;
using AiReadiness.Scanner.Reporting;

namespace AiReadiness.Scanner;

public sealed class ScannerCli(IRepoOpener opener, TimeProvider clock, IAssets assets, IBrowserLauncher? browser = null)
{
    public const int Ok = 0;
    public const int BelowMinimum = 1;
    public const int UsageError = 2;
    public const int CouldNotApply = 3;

    private const string Usage = """
        Usage: ai-readiness [path] [options]

          path              repository folder, relative or absolute (default: current folder)
          --all             treat path as a folder of repositories and rank them all
          --json            print the full report as JSON instead of markdown
          --html            always write the standalone HTML report (it is written and opened by default at a terminal)
          --out <file>      write the HTML report to this file instead of a temporary one
          --no-open         do not open the HTML report in the browser
          --cta-url <url>   add a call-to-action button to the HTML report
          --cta-text <txt>  its label (needs --cta-url)
          --fix             show what the scanner can add to raise the score, plus prompts for the rest
          --apply           with --fix: create the files (never overwrites an existing file)
          --min-score <n>   exit with code 1 when a score is lower than n (for CI)
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

    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Contains("-h") || args.Contains("--help"))
        {
            output.WriteLine(Usage);
            return Ok;
        }

        if (!CliOptions.TryParse(args, out var options, out var problem))
        {
            error.WriteLine($"{problem}{Environment.NewLine}{Usage}");
            return UsageError;
        }

        try
        {
            if (options.Fix)
            {
                return RunFix(options, output, error);
            }

            return options.All ? RunAll(options, output, error) : RunOne(options, output, error);
        }
        catch (DirectoryNotFoundException ex)
        {
            error.WriteLine(ex.Message);
            return UsageError;
        }
    }

    private int RunOne(CliOptions options, TextWriter output, TextWriter error)
    {
        var repo = opener.Open(options.Path);
        var assessment = new ReadinessScanner(DefaultChecks).Scan(repo.Files);

        output.WriteLine(options.Json ? JsonReport.Render(assessment, repo.Name) : MarkdownReport.Render(assessment, repo.Name));
        WriteHtml(options, repo.Name, error, () => HtmlReport.Render(assessment, repo.Name, Today(), options.Cta));

        return options.MinScore is { } required && assessment.Score < required ? BelowMinimum : Ok;
    }

    private int RunFix(CliOptions options, TextWriter output, TextWriter error)
    {
        var repo = opener.Open(options.Path);
        var scanner = new ReadinessScanner(DefaultChecks);
        var planner = new RemediationPlanner(scanner, RemediationPlanner.DefaultFixes, Remediation.Guided.GuidedCatalog.All, assets);
        var plan = planner.Plan(repo.Files, Today());

        if (!options.Apply || plan.AllChanges.Count == 0)
        {
            output.WriteLine(FixPlanReport.RenderPreview(plan, repo.Name));
            return Ok;
        }

        try
        {
            opener.OpenWriter(options.Path).CreateAll(plan.AllChanges);
        }
        catch (FixConflictException ex)
        {
            error.WriteLine(ex.Message);
            return CouldNotApply;
        }

        var after = scanner.Scan(opener.Open(options.Path).Files);
        output.WriteLine(FixPlanReport.RenderApplied(plan, repo.Name, after.Score, after.MaxScore));
        return Ok;
    }

    private int RunAll(CliOptions options, TextWriter output, TextWriter error)
    {
        var scanner = new ReadinessScanner(DefaultChecks);
        var repos = opener.OpenAll(options.Path);
        if (repos.Count == 0)
        {
            error.WriteLine("No Git repositories found directly inside that folder.");
            return UsageError;
        }

        var org = new OrgAssessment(repos.Select(r => new RepoResult(r.Name, scanner.Scan(r.Files))));

        output.WriteLine(options.Json ? JsonReport.RenderOrg(org) : MarkdownReport.RenderOrg(org));
        WriteHtml(options, "organisation", error, () => HtmlReport.RenderOrg(org, Today(), options.Cta));

        return options.MinScore is { } required && org.AnyBelow(required) ? BelowMinimum : Ok;
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>
    /// Writes the HTML report when asked for (--html or --out) or when it will be opened. Opens it at a terminal unless --no-open.
    /// </summary>
    private void WriteHtml(CliOptions options, string name, TextWriter error, Func<string> render)
    {
        var open = browser is not null && !options.NoOpen && !options.Json;
        var wanted = open || options.Html || options.HtmlPath is not null;
        if (!wanted)
        {
            return;
        }

        var path = options.HtmlPath ?? TempReportPath(name);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, render());
        error.WriteLine($"HTML report written to {path}");

        if (open && !browser!.TryOpen(path))
        {
            error.WriteLine("Could not open the browser. Open the file above yourself.");
        }
    }

    private string TempReportPath(string name)
    {
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        return Path.Combine(Path.GetTempPath(), "ai-readiness", $"{safe}-{clock.GetUtcNow():yyyyMMdd-HHmmss}.html");
    }
}
