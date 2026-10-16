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
    [InlineData("release-phase")]
    public void EverySkill_IsEmbeddedWithItsOwnName(string name)
    {
        var content = new EmbeddedAssets().Read(SdlcSkills.AssetName(name));

        Assert.StartsWith($"---\nname: {name}\n", content);
    }

    [Theory]
    [InlineData("requirements-phase", "requirements")]
    [InlineData("design-phase", "design")]
    [InlineData("build-phase", "build")]
    [InlineData("test-phase", "test")]
    [InlineData("release-phase", "release")]
    public void PhaseSkills_CarryTheirPhaseAndTheBranchAndPrRules(string name, string phase)
    {
        var content = new EmbeddedAssets().Read(SdlcSkills.AssetName(name));

        Assert.Contains($"phase: \"{phase}\"", content);
        Assert.Contains("never commit on the default branch", content);
        Assert.Contains("Never push, merge, or open the pull request yourself", content);
        Assert.Contains("Never invent", content);
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
}
