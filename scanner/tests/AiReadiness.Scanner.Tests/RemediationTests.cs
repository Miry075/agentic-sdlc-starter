using System.Text.Json;
using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;
using AiReadiness.Scanner.Remediation.Fixes;
using AiReadiness.Scanner.Remediation.Guided;

namespace AiReadiness.Scanner.Tests;

public class EmbeddedAssetsTests
{
    [Fact]
    public void EveryAsset_IsEmbeddedAndNotEmpty()
    {
        var assets = new EmbeddedAssets();

        Assert.All(AssetNames.All, name => Assert.NotEmpty(assets.Read(name)));
    }

    [Fact]
    public void Scripts_StartWithShebang_AndSettingsAreValidJsonWithGuard()
    {
        var assets = new EmbeddedAssets();

        Assert.StartsWith("#!", assets.Read(AssetNames.HookScript));
        Assert.StartsWith("#!", assets.Read(AssetNames.FreshnessScript));
        Assert.StartsWith("#!", assets.Read(AssetNames.DocsTemplateScript));
        using var settings = JsonDocument.Parse(assets.Read(AssetNames.ProjectSettings));
        Assert.True(settings.RootElement.GetProperty("permissions").TryGetProperty("deny", out _));
        Assert.True(settings.RootElement.GetProperty("hooks").TryGetProperty("PreToolUse", out _));
        Assert.False(settings.RootElement.TryGetProperty("allowedMcpServers", out _));
    }
}

public class RemediationPlannerTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);
    private static readonly ReadinessScanner Scanner = new(ScannerCli.DefaultChecks);
    private static readonly RemediationPlanner Planner = new(Scanner, RemediationPlanner.DefaultFixes, GuidedCatalog.All, new EmbeddedAssets());

    private static RemediationPlan Plan(FakeRepo repo) => Planner.Plan(repo, Today);

    private static int Points(Assessment a, string area) => a.ByArea.Single(x => x.Area.StartsWith(area, StringComparison.Ordinal)).Points;

    [Fact]
    public void Plan_ForEmptyRepository_NeverEarnsPointsForContentOnlyAPersonCanWrite()
    {
        var plan = Plan(new FakeRepo());

        Assert.Equal(0, Points(plan.After, "Agent context"));
        Assert.Equal(0, Points(plan.After, "Domain glossary"));
        Assert.Equal(0, Points(plan.After, "Specs"));
        Assert.Equal(0, Points(plan.After, "Measured quality"));
    }

    [Fact]
    public void Plan_ForEmptyRepository_OffersGuardGitignoreAndFirstAdrOnly()
    {
        var plan = Plan(new FakeRepo());

        Assert.Equal(
            ["Block agents from reading secrets and classified files", "Keep .env files out of Git", "Record the first architecture decision", "Install the /ai-readiness skill for Claude Code"],
            plan.Automatic.Select(f => f.Title));
    }

    [Fact]
    public void Plan_GainsAreMeasuredAndAddUpToTheScoreAfter()
    {
        var plan = Plan(new FakeRepo().With("AGENTS.md", Sample.Agents));

        Assert.True(plan.Automatic.All(f => f.Gain >= 0));
        Assert.Equal(plan.Before.Score + plan.Automatic.Sum(f => f.Gain), plan.After.Score);
    }

    [Fact]
    public void Plan_InstallsTheSkillOnce_AndItEarnsNoPointsByItself()
    {
        var plan = Plan(new FakeRepo());
        var skill = plan.Automatic.Single(f => f.Changes.Any(c => c.Path == ReadinessSkillFix.Path));
        var again = Plan(new FakeRepo().With(ReadinessSkillFix.Path, "mine"));

        Assert.Equal(0, skill.Gain);
        Assert.StartsWith("---\nname: ai-readiness", skill.Changes.Single().Content);
        Assert.DoesNotContain(again.AllChanges, c => c.Path == ReadinessSkillFix.Path);
    }

    [Fact]
    public void Plan_WithoutAgentsFile_DoesNotAddClaudeMdOrCiChecks()
    {
        var plan = Plan(new FakeRepo().With(".github/workflows/x.yml", "name: x"));

        Assert.DoesNotContain(plan.AllChanges, c => c.Path == "CLAUDE.md");
        Assert.DoesNotContain(plan.AllChanges, c => c.Path.StartsWith("scripts/", StringComparison.Ordinal));
        Assert.Contains(plan.Guided, g => g.Title == "Write AGENTS.md");
    }

    [Fact]
    public void Plan_NeverProposesAFileThatAlreadyExists()
    {
        var repo = new FakeRepo()
            .With("AGENTS.md", Sample.Agents)
            .With(".github/x", "")
            .With("CLAUDE.md", "my own")
            .With(".gitignore", "bin/")
            .With(".claude/settings.json", "{}")
            .With("scripts/check-agents-md-freshness.sh", "#!/bin/bash");

        var plan = Plan(repo);

        Assert.All(plan.AllChanges, c => Assert.False(repo.FileExists(c.Path), c.Path));
    }

    [Fact]
    public void Plan_ExistingGitignoreAndSettings_BecomeGuidedSteps()
    {
        var plan = Plan(new FakeRepo().With(".gitignore", "bin/").With(".claude/settings.json", "{}"));

        Assert.Contains(plan.Guided, g => g.Title == "Add .env to .gitignore");
        Assert.Contains(plan.Guided, g => g.Title == "Merge the agent guard into .claude/settings.json");
        Assert.DoesNotContain(plan.AllChanges, c => c.Path is ".gitignore" or ".claude/settings.json");
    }

    [Theory]
    [InlineData(".github/workflows/ci.yml", ".github/workflows/agent-context.yml")]
    [InlineData("azure-pipelines.yml", ".azure-pipelines/agent-context.yml")]
    public void Plan_PicksTheCiSystemTheRepositoryUses(string existing, string expected)
    {
        var plan = Plan(new FakeRepo().With("AGENTS.md", Sample.Agents).With(existing, "x"));

        Assert.Contains(plan.AllChanges, c => c.Path == expected);
        Assert.Contains(plan.AllChanges, c => c.Path == "scripts/check-agents-md-freshness.sh" && c.Executable);
    }

    [Fact]
    public void Plan_ReadsTheGitRemoteWhenThereIsNoPipelineYet()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents).With(".git/config", "[remote \"origin\"]\n\turl = https://dev.azure.com/o/p/_git/r");

        Assert.Contains(Plan(repo).AllChanges, c => c.Path == ".azure-pipelines/agent-context.yml");
    }

    [Fact]
    public void Plan_WhenCiSystemIsUnknown_LeavesCiToAGuidedStep()
    {
        var plan = Plan(new FakeRepo().With("AGENTS.md", Sample.Agents));

        Assert.DoesNotContain(plan.AllChanges, c => c.Path.Contains("agent-context", StringComparison.Ordinal));
        Assert.Contains(plan.Guided, g => g.Title == "Add the CI checks");
    }

    [Fact]
    public void Plan_FirstAdrIsDatedToday_AndSkippedWhenAdrsExist()
    {
        var fresh = Plan(new FakeRepo());
        var adr = fresh.AllChanges.Single(c => c.Path == "docs/adr/0001-record-architecture-decisions.md");
        var existing = Plan(new FakeRepo().With("docs/adr/0001-something.md", "Status: Accepted"));

        Assert.Contains("2026-10-16", adr.Content);
        Assert.DoesNotContain("2026-01-01", adr.Content);
        Assert.DoesNotContain(existing.AllChanges, c => c.Path.StartsWith("docs/adr/", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_GuidedStepsCoverOnlyWhatIsStillFailing_HeaviestFirst()
    {
        var plan = Plan(new FakeRepo().With("AGENTS.md", Sample.Agents).With("CLAUDE.md", "@AGENTS.md").With("docs/domain/glossary.md", Sample.Glossary(15)));

        Assert.DoesNotContain(plan.Guided, g => g.Title is "Write AGENTS.md" or "Write the domain glossary");
        Assert.Equal(plan.Guided.OrderByDescending(g => g.PotentialGain).Select(g => g.Title), plan.Guided.Select(g => g.Title));
        Assert.True(plan.PotentialScore <= plan.After.MaxScore);
    }

    [Fact]
    public void Plan_GuidedPromptsForbidInventingFacts()
    {
        var plan = Plan(new FakeRepo());

        Assert.All(plan.Guided.Where(g => g.Title is "Write AGENTS.md" or "Write the domain glossary" or "Write the first specs"),
            g => Assert.Contains("Do not invent", g.Prompt));
    }

    [Fact]
    public void Plan_SpecAndAdrPromptsUseTheHeadingsTheCiCheckRequires()
    {
        var plan = Plan(new FakeRepo());
        var specs = plan.Guided.Single(g => g.Title == "Write the first specs").Prompt;
        var adrs = Plan(new FakeRepo().With("docs/adr/0001-a.md", "## Status\n\nAccepted")).Guided.Single(g => g.Title == "Record more architecture decisions").Prompt;

        Assert.All(["Goal", "Acceptance criteria", "Non-goals"], h => Assert.Contains(h, specs));
        Assert.All(["## Status", "## Context", "## Decision", "## Consequences"], h => Assert.Contains(h, adrs));
        Assert.DoesNotContain("Status: Accepted", adrs);
    }

    [Fact]
    public void Plan_AfterApplyingEverything_FindsNothingMoreToCreate()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents).With(".github/x", "");
        var first = Plan(repo);
        repo.CreateAll(first.AllChanges);

        var second = Plan(repo);

        Assert.Empty(second.Automatic);
        Assert.Equal(first.After.Score, second.Before.Score);
    }
}

public class OverlayRepoFilesTests
{
    [Fact]
    public void Overlay_ShowsPlannedFilesWithoutTouchingTheBase()
    {
        var baseRepo = new FakeRepo().With("a.md", "base");
        var overlay = new OverlayRepoFiles(baseRepo, [new FileChange("docs/new.md", "new")]);

        Assert.True(overlay.FileExists("docs/new.md"));
        Assert.True(overlay.DirectoryExists("docs"));
        Assert.Equal("new", overlay.ReadText("docs/new.md"));
        Assert.Equal("base", overlay.ReadText("a.md"));
        Assert.Equal(["docs/new.md"], overlay.Files("docs"));
        Assert.False(baseRepo.FileExists("docs/new.md"));
    }
}

public class DiskRepoWriterTests
{
    [Fact]
    public void CreateAll_WritesFilesWithDirectoriesAndExecutableBit()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Path);

        new DiskRepoWriter(dir.Path).CreateAll([new FileChange(".claude/hooks/run.sh", "#!/bin/sh\n", Executable: true), new FileChange("CLAUDE.md", "@AGENTS.md\n")]);

        Assert.Equal("@AGENTS.md\n", File.ReadAllText(System.IO.Path.Combine(dir.Path, "CLAUDE.md")));
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(System.IO.Path.Combine(dir.Path, ".claude/hooks/run.sh")).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public void CreateAll_WhenAnyTargetExists_WritesNothingAndKeepsTheOriginal()
    {
        using var dir = new TempDir().Write("CLAUDE.md", "mine");

        var ex = Assert.Throws<FixConflictException>(() =>
            new DiskRepoWriter(dir.Path).CreateAll([new FileChange("new.md", "x"), new FileChange("CLAUDE.md", "theirs")]));

        Assert.Contains("CLAUDE.md", ex.Message);
        Assert.Equal("mine", File.ReadAllText(System.IO.Path.Combine(dir.Path, "CLAUDE.md")));
        Assert.False(File.Exists(System.IO.Path.Combine(dir.Path, "new.md")));
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("docs/../../escape.md")]
    public void CreateAll_OutsideTheRoot_IsRefused(string path)
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Path);

        Assert.Throws<FixConflictException>(() => new DiskRepoWriter(dir.Path).CreateAll([new FileChange(path, "x")]));
    }

    [Fact]
    public void CreateAll_ThroughASymbolicLinkedFolder_IsRefused()
    {
        using var dir = new TempDir().Write("real/keep.txt", "x");
        using var outside = new TempDir();
        Directory.CreateDirectory(outside.Path);
        try
        {
            Directory.CreateSymbolicLink(System.IO.Path.Combine(dir.Path, "linked"), outside.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        Assert.Throws<FixConflictException>(() => new DiskRepoWriter(dir.Path).CreateAll([new FileChange("linked/pwned.md", "x")]));
        Assert.False(File.Exists(System.IO.Path.Combine(outside.Path, "pwned.md")));
    }
}
