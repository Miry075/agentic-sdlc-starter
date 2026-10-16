using AiReadiness.Scanner.Checks;
using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;
using AiReadiness.Scanner.Reporting;

namespace AiReadiness.Scanner;

public sealed class ScannerCli(IRepoOpener opener, TimeProvider clock, IAssets assets, IBrowserLauncher? browser = null, IAgentRunner? agent = null, IGit? git = null)
{
    public const int Ok = 0;
    public const int BelowMinimum = 1;
    public const int UsageError = 2;
    public const int CouldNotApply = 3;
    public const int AgentFailed = 4;
    public const int BranchPolicyViolated = 5;

    private const string Usage = """
        Usage: ai-readiness [path] [options]
               ai-readiness agent [path] [--dry-run]
               ai-readiness sdlc [path] [--apply]

          sdlc              install the SDLC phase skills (/sdlc, /requirements-phase ... /release-phase); --apply writes them

          agent             run the /ai-readiness skill unattended in Claude Code (uses your Claude login)

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
          --version         print the installed version
        """;

    public static string Version { get; } =
        (typeof(ScannerCli).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "unknown")
        .Split('+')[0];

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
        if (args.Contains("--version"))
        {
            output.WriteLine(Version);
            return Ok;
        }

        if (args.Contains("-h") || args.Contains("--help"))
        {
            output.WriteLine(Usage);
            return Ok;
        }

        if (args.Length > 0 && args[0] == "agent")
        {
            return RunAgent(args[1..], output, error);
        }

        if (args.Length > 0 && args[0] == "sdlc")
        {
            return RunSdlc(args[1..], output, error);
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
            output.WriteLine(options.Json ? JsonReport.RenderPlan(plan, repo.Name, appliedScore: null) : FixPlanReport.RenderPreview(plan, repo.Name));
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
        output.WriteLine(options.Json
            ? JsonReport.RenderPlan(plan, repo.Name, after.Score)
            : FixPlanReport.RenderApplied(plan, repo.Name, after.Score, after.MaxScore));
        return Ok;
    }

    private int RunAgent(string[] args, TextWriter output, TextWriter error)
    {
        var path = ".";
        var dryRun = false;
        foreach (var arg in args)
        {
            if (arg == "--dry-run")
            {
                dryRun = true;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error.WriteLine($"Unknown option {arg} for agent.{Environment.NewLine}{Usage}");
                return UsageError;
            }
            else if (path != ".")
            {
                error.WriteLine($"Give one repository path, not two ('{path}' and '{arg}').");
                return UsageError;
            }
            else
            {
                path = arg;
            }
        }

        RepoEntry repo;
        try
        {
            repo = opener.Open(path);
        }
        catch (DirectoryNotFoundException ex)
        {
            error.WriteLine(ex.Message);
            return UsageError;
        }

        if (!repo.Files.DirectoryExists(".git") && !repo.Files.FileExists(".git"))
        {
            error.WriteLine("The agent works on a branch, so the folder must be a Git repository.");
            return UsageError;
        }

        var root = Path.GetFullPath(path);
        if (git is null)
        {
            error.WriteLine("Git is not available.");
            return UsageError;
        }

        if (git.DefaultBranch(root) is not { } defaultBranch)
        {
            error.WriteLine("Could not find the default branch (origin/HEAD, main or master). Create it or run `git remote set-head origin --auto`.");
            return UsageError;
        }

        var featureBranch = FeatureBranchName(root, git);
        var installSkill = !repo.Files.FileExists(Remediation.Fixes.ReadinessSkillFix.Path);
        var sessionId = Guid.NewGuid();
        if (dryRun)
        {
            output.WriteLine($"Would create {featureBranch} from {defaultBranch} and switch to it.");
            if (installSkill)
            {
                output.WriteLine($"Would install {Remediation.Fixes.ReadinessSkillFix.Path} on that branch.");
            }

            output.WriteLine($"Would run in {root}:");
            output.WriteLine(AgentCommand.Display(sessionId, featureBranch));
            return Ok;
        }

        if (agent is null || !agent.IsAvailable())
        {
            error.WriteLine("Claude Code (`claude`) is not installed or not on PATH. Install it from https://code.claude.com and log in once with `claude`.");
            return UsageError;
        }

        if (!git.IsClean(root))
        {
            error.WriteLine("The repository has uncommitted changes. Commit or stash them first, so they do not end up in the AI-readiness branch.");
            return UsageError;
        }

        var defaultTip = git.Tip(root, defaultBranch);
        if (!git.CreateAndSwitch(root, featureBranch, defaultBranch))
        {
            error.WriteLine($"Could not create {featureBranch} from {defaultBranch}.");
            return UsageError;
        }

        output.WriteLine($"Created {featureBranch} from {defaultBranch}. Every change goes there; {defaultBranch} only changes through your pull request.");

        if (installSkill)
        {
            opener.OpenWriter(path).CreateAll([new FileChange(Remediation.Fixes.ReadinessSkillFix.Path, assets.Read(AssetNames.ReadinessSkill))]);
            if (!git.CommitFile(root, Remediation.Fixes.ReadinessSkillFix.Path, "AI-readiness: install the /ai-readiness skill"))
            {
                error.WriteLine($"Installed {Remediation.Fixes.ReadinessSkillFix.Path} but could not commit it. Commit it on {featureBranch}, then run this again.");
                return UsageError;
            }

            output.WriteLine($"Installed and committed {Remediation.Fixes.ReadinessSkillFix.Path} on {featureBranch}.");
        }

        var scanner = new ReadinessScanner(DefaultChecks);
        var before = scanner.Scan(repo.Files).Score;
        output.WriteLine($"Running the /ai-readiness skill in Claude Code on {repo.Name}. This can take a while and uses your Claude plan.");
        output.WriteLine($"Session {sessionId}. Each step is logged below.");
        output.WriteLine();
        var code = agent.Run(root, AgentCommand.Arguments(sessionId, featureBranch), line =>
        {
            var stamp = clock.GetLocalNow().ToString("HH:mm:ss");
            foreach (var entry in AgentProgress.Describe(line))
            {
                output.WriteLine(entry.Length == 0 ? string.Empty : $"[{stamp}] {entry}");
            }

            output.Flush();
        });

        var policyKept = git.Tip(root, defaultBranch) == defaultTip && git.CurrentBranch(root) == featureBranch;
        if (!policyKept)
        {
            error.WriteLine($"Branch policy broken: {defaultBranch} moved or the run left {featureBranch}. Check `git log --oneline -5 {defaultBranch}` before doing anything else.");
        }

        if (code != 0)
        {
            error.WriteLine($"Claude Code stopped with exit code {code}. If it reported an unknown option, update it with `claude update`, then run this again.");
            return AgentFailed;
        }

        var after = scanner.Scan(opener.Open(path).Files);
        output.WriteLine();
        output.WriteLine($"AI-readiness: {before}/{after.MaxScore} to {after.Score}/{after.MaxScore}");
        WriteHtml(new CliOptions(path, false, false, null, null, null), repo.Name, error, () => HtmlReport.Render(after, repo.Name, Today(), null));
        output.WriteLine();
        output.WriteLine($"Review the branch, then send it as a pull request into {defaultBranch}:");
        output.WriteLine($"  git log --oneline {defaultBranch}..{featureBranch}");
        output.WriteLine($"  git push -u origin {featureBranch}");
        output.WriteLine(git.Host(root) switch
        {
            "azure" => $"  az repos pr create --source-branch {featureBranch} --target-branch {defaultBranch} --title \"AI-readiness: {before} to {after.Score}\"",
            "github" => $"  gh pr create --base {defaultBranch} --head {featureBranch} --title \"AI-readiness: {before} to {after.Score}\" --fill",
            _ => "  then open the pull request on your Git server.",
        });
        output.WriteLine("The full pull request description is at the end of the log above.");
        output.WriteLine();
        output.WriteLine("To answer its questions, resume this exact run (claude --continue would open a different conversation):");
        output.WriteLine($"  cd \"{root}\"");
        output.WriteLine($"  {AgentCommand.ResumeCommand(sessionId)}");
        return policyKept ? Ok : BranchPolicyViolated;
    }

    private int RunSdlc(string[] args, TextWriter output, TextWriter error)
    {
        var path = ".";
        var apply = false;
        foreach (var arg in args)
        {
            if (arg == "--apply")
            {
                apply = true;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error.WriteLine($"Unknown option {arg} for sdlc.{Environment.NewLine}{Usage}");
                return UsageError;
            }
            else if (path != ".")
            {
                error.WriteLine($"Give one repository path, not two ('{path}' and '{arg}').");
                return UsageError;
            }
            else
            {
                path = arg;
            }
        }

        RepoEntry repo;
        try
        {
            repo = opener.Open(path);
        }
        catch (DirectoryNotFoundException ex)
        {
            error.WriteLine(ex.Message);
            return UsageError;
        }

        var missing = SdlcSkills.Names.Where(n => !repo.Files.FileExists(SdlcSkills.RepoPath(n))).ToList();
        output.WriteLine($"# SDLC skills for `{repo.Name}`");
        output.WriteLine();
        foreach (var name in SdlcSkills.Names)
        {
            output.WriteLine(missing.Contains(name)
                ? $"- {(apply ? "create" : "will create")} `{SdlcSkills.RepoPath(name)}` (/{name})"
                : $"- keep `{SdlcSkills.RepoPath(name)}` (already there, never overwritten)");
        }

        output.WriteLine();
        if (missing.Count == 0)
        {
            output.WriteLine("Nothing to install.");
            return Ok;
        }

        if (!apply)
        {
            output.WriteLine("Nothing has been written. Run again with `--apply`, on a feature branch.");
            return Ok;
        }

        var root = Path.GetFullPath(path);
        if (git is not null && git.DefaultBranch(root) is { } defaultBranch && git.CurrentBranch(root) == defaultBranch)
        {
            error.WriteLine($"You are on {defaultBranch}. Create a feature branch first, then run this again:");
            error.WriteLine($"  git switch -c feature/sdlc-skills {defaultBranch}");
            return UsageError;
        }

        try
        {
            opener.OpenWriter(path).CreateAll(missing.Select(n => new FileChange(SdlcSkills.RepoPath(n), assets.Read(SdlcSkills.AssetName(n)))).ToList());
        }
        catch (FixConflictException ex)
        {
            error.WriteLine(ex.Message);
            return CouldNotApply;
        }

        output.WriteLine("Installed. Next:");
        output.WriteLine("  git add .claude/skills && git commit -m \"Add SDLC phase skills\"");
        output.WriteLine("  push the branch and open a pull request");
        output.WriteLine("Then, in Claude Code: /sdlc <work item id> runs the next phase for that work item.");
        return Ok;
    }

    private string FeatureBranchName(string root, IGit git)
    {
        var name = $"feature/ai-readiness-{clock.GetLocalNow():yyyy-MM-dd}";
        var candidate = name;
        for (var i = 2; git.BranchExists(root, candidate); i++)
        {
            candidate = $"{name}-{i}";
        }

        return candidate;
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
