using System.Text.RegularExpressions;
using AiReadiness.Scanner.Checks;

namespace AiReadiness.Scanner.Remediation.Fixes;

/// <summary>Creates .claude/settings.json with deny rules and the PreToolUse hook that blocks classified files.</summary>
public sealed class ClaudeGuardFix : IAutomaticRemediation
{
    private const string Settings = ".claude/settings.json";
    private const string Hook = ".claude/hooks/block-classified.sh";
    private const string Patterns = ".claude/classified-paths.txt";

    public ProposedFix? Propose(RemediationContext context)
    {
        if (context.Repo.FileExists(Settings))
        {
            return null; // merging into an existing file is a guided step
        }

        var changes = new List<FileChange> { new(Settings, context.Assets.Read(AssetNames.ProjectSettings)) };
        if (!context.Repo.FileExists(Hook))
        {
            changes.Add(new FileChange(Hook, context.Assets.Read(AssetNames.HookScript), Executable: true));
        }

        if (!context.Repo.FileExists(Patterns))
        {
            changes.Add(new FileChange(Patterns, context.Assets.Read(AssetNames.ClassifiedPaths)));
        }

        return new ProposedFix("Block agents from reading secrets and classified files", changes,
            ["The hook needs `jq` installed on each developer machine.", "Edit .claude/classified-paths.txt to match your repository."]);
    }
}

/// <summary>Creates a .gitignore that excludes .env files, only when the repository has no .gitignore at all.</summary>
public sealed class GitignoreFix : IAutomaticRemediation
{
    public ProposedFix? Propose(RemediationContext context)
    {
        if (context.Repo.FileExists(".gitignore"))
        {
            return null; // adding lines to an existing file is a guided step
        }

        const string content = "# Secrets: never commit these\n.env\n.env.*\n!.env.example\n";
        return new ProposedFix("Keep .env files out of Git", [new FileChange(".gitignore", content)], []);
    }
}

/// <summary>Adds the ADR template and the standard first decision (record decisions in the repo) when there are no ADRs.</summary>
public sealed class FirstAdrFix : IAutomaticRemediation
{
    private const string Template = "docs/adr/0000-template.md";
    private const string First = "docs/adr/0001-record-architecture-decisions.md";

    public ProposedFix? Propose(RemediationContext context)
    {
        if (AdrCheck.RealAdrs(context.Repo).Count > 0)
        {
            return null;
        }

        var changes = new List<FileChange>();
        if (!context.Repo.FileExists(Template))
        {
            changes.Add(new FileChange(Template, context.Assets.Read(AssetNames.AdrTemplate)));
        }

        var first = context.Assets.Read(AssetNames.FirstAdr).Replace("2026-01-01", context.Today.ToString("yyyy-MM-dd"));
        changes.Add(new FileChange(First, first));

        return new ProposedFix("Record the first architecture decision", changes,
            ["ADR 0001 is marked Accepted and dated today. Change the deciders to the real ones."]);
    }
}

/// <summary>Adds CLAUDE.md with the single line that makes every agent read AGENTS.md. Only when AGENTS.md exists.</summary>
public sealed class ClaudeMdFix : IAutomaticRemediation
{
    public ProposedFix? Propose(RemediationContext context)
    {
        if (!context.Repo.FileExists("AGENTS.md") || context.Repo.FileExists("CLAUDE.md"))
        {
            return null;
        }

        return new ProposedFix("Point CLAUDE.md at AGENTS.md", [new FileChange("CLAUDE.md", "@AGENTS.md\n")], []);
    }
}

/// <summary>Adds the CI checks that fail a pull request when the structure changes without AGENTS.md. Only when AGENTS.md exists and the CI platform is clear.</summary>
public sealed partial class CiChecksFix : IAutomaticRemediation
{
    private const string FreshnessScript = "scripts/check-agents-md-freshness.sh";
    private const string DocsScript = "scripts/check-docs-templates.sh";
    private const string GithubWorkflow = ".github/workflows/agent-context.yml";
    private const string AzurePipeline = ".azure-pipelines/agent-context.yml";

    public ProposedFix? Propose(RemediationContext context)
    {
        var repo = context.Repo;
        if (!repo.FileExists("AGENTS.md"))
        {
            return null;
        }

        var remotes = RemoteUrls(repo.ReadText(".git/config") ?? string.Empty);
        var github = repo.DirectoryExists(".github") || remotes.Any(u => u.Contains("github.com", StringComparison.OrdinalIgnoreCase));
        var azure = repo.FileExists("azure-pipelines.yml") || repo.DirectoryExists(".azure-pipelines")
                    || remotes.Any(u => u.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) || u.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase));
        if (github == azure)
        {
            return null; // unknown or both: a guided step decides
        }

        var pipelinePath = github ? GithubWorkflow : AzurePipeline;
        if (repo.FileExists(pipelinePath))
        {
            return null;
        }

        var changes = new List<FileChange>
        {
            new(pipelinePath, context.Assets.Read(github ? AssetNames.GithubWorkflow : AssetNames.AzurePipeline)),
        };
        if (!repo.FileExists(FreshnessScript))
        {
            changes.Add(new FileChange(FreshnessScript, context.Assets.Read(AssetNames.FreshnessScript), Executable: true));
        }

        if (!repo.FileExists(DocsScript))
        {
            changes.Add(new FileChange(DocsScript, context.Assets.Read(AssetNames.DocsTemplateScript), Executable: true));
        }

        List<string> notes =
        [
            "Pull requests that change the project structure without updating AGENTS.md will fail this check.",
            "ADRs and specs must follow their templates (docs/adr/0000-template.md, docs/specs/TEMPLATE.md).",
        ];
        notes.Add(github
            ? "Skip the structure check for one pull request with the `skip-agents-md` label."
            : "Register .azure-pipelines/agent-context.yml as a pipeline and add it as a build validation policy on main.");

        return new ProposedFix("Add the CI checks that keep AGENTS.md fresh", changes, notes);
    }

    private static IEnumerable<string> RemoteUrls(string gitConfig) =>
        RemoteUrl().Matches(gitConfig).Select(m => m.Groups[1].Value);

    [GeneratedRegex(@"^\s*url\s*=\s*(\S+)", RegexOptions.Multiline)]
    private static partial Regex RemoteUrl();
}
