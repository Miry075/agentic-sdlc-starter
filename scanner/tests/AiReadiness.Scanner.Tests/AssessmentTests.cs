using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Tests;

public class AssessmentTests
{
    [Theory]
    [InlineData(100, ReadinessBand.Strong)]
    [InlineData(90, ReadinessBand.Strong)]
    [InlineData(89, ReadinessBand.Ready)]
    [InlineData(70, ReadinessBand.Ready)]
    [InlineData(69, ReadinessBand.Partial)]
    [InlineData(40, ReadinessBand.Partial)]
    [InlineData(39, ReadinessBand.NotReady)]
    [InlineData(0, ReadinessBand.NotReady)]
    public void Band_ForScore_UsesDocumentedThresholds(int score, ReadinessBand expected)
    {
        var assessment = new Assessment(
        [
            new Criterion("A", "lost", 100 - score, Passed: false, "x"),
            new Criterion("A", "points", score, Passed: true, "x"),
        ]);

        Assert.Equal(score, assessment.Score);
        Assert.Equal(expected, assessment.Band);
    }

    [Fact]
    public void Gaps_WhenSeveralFail_ListsHeaviestFirstAndSkipsPassed()
    {
        var assessment = new Assessment(
        [
            new Criterion("A", "small", 2, false, "x"),
            new Criterion("A", "done", 9, true, "x"),
            new Criterion("A", "big", 7, false, "x"),
        ]);

        Assert.Equal(["big", "small"], assessment.Gaps.Select(g => g.Name));
    }

    [Fact]
    public void DefaultChecks_WeightsAddUpToOneHundred()
    {
        var all = new ReadinessScanner(ScannerCli.DefaultChecks).Scan(new FakeRepo());

        Assert.Equal(100, all.MaxScore);
    }

    [Fact]
    public void Scan_EmptyRepository_ScoresOnlyForHavingNoLeaks()
    {
        var assessment = new ReadinessScanner(ScannerCli.DefaultChecks).Scan(new FakeRepo());

        Assert.Equal(14, assessment.Score);
        Assert.Equal(ReadinessBand.NotReady, assessment.Band);
    }

    [Fact]
    public void Scan_RepositoryWithEveryPracticeInPlace_ScoresFullMarks()
    {
        var repo = new FakeRepo()
            .With("AGENTS.md", Sample.Agents)
            .With("CLAUDE.md", "@AGENTS.md")
            .With("docs/domain/glossary.md", Sample.Glossary(15))
            .With("docs/adr/0000-template.md", "# template")
            .With("docs/adr/0001-a.md", "Status: Accepted")
            .With("docs/adr/0002-b.md", "Status: Proposed")
            .With("docs/adr/0003-c.md", "Status: Accepted")
            .With("docs/specs/TEMPLATE.md", "# t")
            .With("docs/specs/42.md", "## Acceptance criteria")
            .With(".github/workflows/ci.yml", "run: bash scripts/check-agents-md-freshness.sh\nrun: bash scripts/check-docs-templates.sh")
            .With(".gitignore", ".env\n.env.*")
            .With(".claude/settings.json", "{ \"permissions\": { \"deny\": [\"Read(./.env)\"] }, \"hooks\": { \"PreToolUse\": [] } }")
            .With("evals/questions.yaml", Sample.Questions(30));

        var assessment = new ReadinessScanner(ScannerCli.DefaultChecks).Scan(repo);

        Assert.Empty(assessment.Gaps);
        Assert.Equal(ReadinessBand.Strong, assessment.Band);
    }
}
