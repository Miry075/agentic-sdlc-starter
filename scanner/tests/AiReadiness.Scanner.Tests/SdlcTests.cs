using AiReadiness.Scanner.Remediation;

namespace AiReadiness.Scanner.Tests;

public class SdlcSkillsTests
{
    private static readonly TimeProvider Clock = new FixedClock(new DateTimeOffset(2026, 10, 16, 8, 0, 0, TimeSpan.Zero));

    private static (int Code, string Out, string Err) Run(FakeRepo repo, FakeGit git, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = new ScannerCli(new FakeOpener(("repo", repo)), Clock, new EmbeddedAssets(), git: git).Run(["sdlc", .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Theory]
    [InlineData("sdlc")]
    [InlineData("requirements-phase")]
    [InlineData("design-phase")]
    [InlineData("build-phase")]
    [InlineData("test-phase")]
    [InlineData("deliver-phase")]
    [InlineData("release-phase")]
    [InlineData("azure-workitem-flow")]
    [InlineData("dotnet-change-guardrails")]
    [InlineData("dotnet-design-principles")]
    [InlineData("dotnet-testing-standards")]
    [InlineData("resolve-review-feedback")]
    [InlineData("legacy-discovery")]
    public void EverySkill_IsEmbeddedWithItsOwnName(string name)
    {
        var content = new EmbeddedAssets().Read(SdlcSkills.AssetName(name));

        Assert.Matches(new System.Text.RegularExpressions.Regex($"^---\\nname: \"?{name}\"?\\n"), content);
    }

    [Fact]
    public void PublishedSkills_CarryNoPersonalOrEmployerContext()
    {
        var content = new EmbeddedAssets().Read(SdlcSkills.AssetName("azure-workitem-flow"));

        Assert.DoesNotContain("The user is a senior", content);
        Assert.DoesNotContain("this health-sector project", content);
    }

    [Theory]
    [InlineData("requirements-phase", "requirements")]
    [InlineData("design-phase", "design")]
    [InlineData("build-phase", "build")]
    [InlineData("test-phase", "test")]
    [InlineData("deliver-phase", "deliver")]
    [InlineData("release-phase", "release")]
    public void PhaseSkills_FollowTheWorkItemFlowRules(string name, string phase)
    {
        var content = new EmbeddedAssets().Read(SdlcSkills.AssetName(name));

        Assert.Contains($"phase: \"{phase}\"", content);
        Assert.Contains("never commit on the base branch", content);
        Assert.Contains("(AB#<work-item-id>)", content);
        Assert.Contains("Never merge", content);
        Assert.Contains("never move a work item to Closed", content);
        Assert.Contains("Never invent", content);
        Assert.Contains("dotnet-change-guardrails", content);
        Assert.Contains("dotnet-design-principles", content);
        Assert.Contains("dotnet-testing-standards", content);
        Assert.Contains("resolve-review-feedback", content);
    }

    [Theory]
    [InlineData("requirements-phase", "Existing AC checklist, first")]
    [InlineData("requirements-phase", "model: \"opus\"")]
    [InlineData("design-phase", "**design fit**")]
    [InlineData("design-phase", "**testability**")]
    [InlineData("build-phase", "**Gate:** ask the user to confirm you should implement")]
    [InlineData("test-phase", "Health-sector sweep")]
    [InlineData("test-phase", "unverified, not built")]
    [InlineData("deliver-phase", "Ask who opens the pull request")]
    [InlineData("deliver-phase", "Completed (Hours)")]
    public void PhaseSkills_KeepTheGatesOfAzureWorkitemFlow(string name, string text)
    {
        Assert.Contains(text, new EmbeddedAssets().Read(SdlcSkills.AssetName(name)));
    }

    [Fact]
    public void Sdlc_Preview_ListsTheSkillsAndWritesNothing()
    {
        var repo = new FakeRepo();

        var (code, output, _) = Run(repo, new FakeGit { Current = "feature/x" });

        Assert.Equal(ScannerCli.Ok, code);
        Assert.Contains("will create `.claude/skills/design-phase/SKILL.md`", output);
        Assert.Contains("Nothing has been written", output);
        Assert.False(repo.FileExists(SdlcSkills.RepoPath("sdlc")));
    }

    [Fact]
    public void Sdlc_Apply_OnAFeatureBranch_CreatesTheMissingSkillsAndKeepsExistingOnes()
    {
        var repo = new FakeRepo().With(SdlcSkills.RepoPath("build-phase"), "my version");

        var (code, output, _) = Run(repo, new FakeGit { Current = "feature/sdlc-skills" }, "--apply");

        Assert.Equal(ScannerCli.Ok, code);
        Assert.All(SdlcSkills.Names, n => Assert.True(repo.FileExists(SdlcSkills.RepoPath(n)), n));
        Assert.Equal("my version", repo.ReadText(SdlcSkills.RepoPath("build-phase")));
        Assert.Contains("keep `.claude/skills/build-phase/SKILL.md`", output);
    }

    [Fact]
    public void Sdlc_Apply_OnTheDefaultBranch_RefusesAndWritesNothing()
    {
        var repo = new FakeRepo();

        var (code, _, error) = Run(repo, new FakeGit { Current = "main" }, "--apply");

        Assert.Equal(ScannerCli.UsageError, code);
        Assert.Contains("git switch -c feature/sdlc-skills main", error);
        Assert.False(repo.FileExists(SdlcSkills.RepoPath("sdlc")));
    }

    [Fact]
    public void Sdlc_WhenEverythingIsInstalled_HasNothingToDo()
    {
        var repo = new FakeRepo();
        foreach (var n in SdlcSkills.Names)
        {
            repo.With(SdlcSkills.RepoPath(n), "x");
        }

        Assert.Contains("Nothing to install", Run(repo, new FakeGit()).Out);
    }

    [Theory]
    [InlineData("Evidence or nothing")]
    [InlineData("`Certain`")]
    [InlineData("`Inferred`")]
    [InlineData("`Unclear`")]
    [InlineData("Read-only")]
    [InlineData("Never commit on the default branch")]
    [InlineData("Status starts as `Proposed`")]
    [InlineData("Do not start any implementation")]
    [InlineData("**existing acceptance criteria**")]
    public void LegacyDiscovery_KeepsEvidenceConfidenceAndTheValidationGate(string text)
    {
        Assert.Contains(text, new EmbeddedAssets().Read(SdlcSkills.AssetName("legacy-discovery")));
    }
}
