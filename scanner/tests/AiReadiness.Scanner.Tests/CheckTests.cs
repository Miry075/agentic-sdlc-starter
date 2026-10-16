using AiReadiness.Scanner.Checks;

namespace AiReadiness.Scanner.Tests;

public class AgentContextCheckTests
{
    private readonly AgentContextCheck _check = new();

    [Fact]
    public void Evaluate_WithoutAgentsFile_FailsEveryCriterionAboutIt()
    {
        var result = _check.Evaluate(new FakeRepo());

        Assert.All(result, c => Assert.False(c.Passed));
    }

    [Fact]
    public void Evaluate_WithCompleteFile_Passes()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents).With("CLAUDE.md", "@AGENTS.md");

        Assert.All(_check.Evaluate(repo), c => Assert.True(c.Passed, c.Name));
    }

    [Theory]
    [InlineData("dotnet run --project src/<Project>.Api")]
    [InlineData("**Last reviewed:** <YYYY-MM-DD>")]
    [InlineData("**Owner:** <team name>")]
    public void Evaluate_WithTemplatePlaceholder_FailsPlaceholderCriterion(string line)
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents + "\n" + line);

        Assert.False(Sample.Passed(_check.Evaluate(repo), "No unfilled"));
    }

    [Fact]
    public void Evaluate_WithGenericTypesInCode_DoesNotCountThemAsPlaceholders()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents + "\nUse `List<string>` and `Task<int>`.");

        Assert.True(Sample.Passed(_check.Evaluate(repo), "No unfilled"));
    }

    [Fact]
    public void Evaluate_WithVeryLongFile_FailsShortnessCriterion()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents + string.Concat(Enumerable.Repeat("\nline", 400)));

        Assert.False(Sample.Passed(_check.Evaluate(repo), "Short enough"));
    }

    [Fact]
    public void Evaluate_WhenClaudeFileDoesNotImportAgents_FailsPointerCriterion()
    {
        var repo = new FakeRepo().With("AGENTS.md", Sample.Agents).With("CLAUDE.md", "my own rules");

        Assert.False(Sample.Passed(_check.Evaluate(repo), "CLAUDE.md"));
    }
}

public class GlossaryCheckTests
{
    private readonly GlossaryCheck _check = new();

    [Theory]
    [InlineData(4, false, false)]
    [InlineData(5, true, false)]
    [InlineData(15, true, true)]
    public void Evaluate_CountsRealTerms(int terms, bool five, bool fifteen)
    {
        var repo = new FakeRepo().With(GlossaryCheck.Path, Sample.Glossary(terms));

        var result = _check.Evaluate(repo);

        Assert.Equal(five, Sample.Passed(result, "At least 5"));
        Assert.Equal(fifteen, Sample.Passed(result, "At least 15"));
    }

    [Fact]
    public void Evaluate_IgnoresPlaceholderRows()
    {
        var rows = string.Concat(Enumerable.Repeat("| **<Term>** | <definition> |\n", 10));
        var repo = new FakeRepo().With(GlossaryCheck.Path, rows);

        Assert.False(Sample.Passed(_check.Evaluate(repo), "At least 5"));
    }
}

public class AdrCheckTests
{
    private readonly AdrCheck _check = new();

    [Fact]
    public void Evaluate_DoesNotCountTheTemplate()
    {
        var repo = new FakeRepo().With("docs/adr/0000-template.md", "Status: Accepted");

        Assert.False(Sample.Passed(_check.Evaluate(repo), "At least one ADR in"));
    }

    [Fact]
    public void Evaluate_WithAcceptedDecision_PassesStatusCriterion()
    {
        var repo = new FakeRepo().With("docs/adr/0001-x.md", "## Status\n\nStatus: Accepted");

        var result = _check.Evaluate(repo);

        Assert.True(Sample.Passed(result, "At least one ADR in"));
        Assert.True(Sample.Passed(result, "At least one ADR is accepted"));
        Assert.False(Sample.Passed(result, "At least three"));
    }
}

public class SpecCheckTests
{
    private readonly SpecCheck _check = new();

    [Fact]
    public void Evaluate_OnlyTemplate_FailsAll()
    {
        var repo = new FakeRepo().With("docs/specs/TEMPLATE.md", "Acceptance criteria");

        Assert.All(_check.Evaluate(repo), c => Assert.False(c.Passed));
    }

    [Fact]
    public void Evaluate_SpecNamedByWorkItemWithCriteria_Passes()
    {
        var repo = new FakeRepo().With("docs/specs/42-vat.md", "## Acceptance criteria\n1. x");

        Assert.All(_check.Evaluate(repo), c => Assert.True(c.Passed, c.Name));
    }
}

public class CiFreshnessCheckTests
{
    private readonly CiFreshnessCheck _check = new();

    [Fact]
    public void Evaluate_WithGithubWorkflowRunningBothScripts_Passes()
    {
        var repo = new FakeRepo().With(".github/workflows/a.yml", "check-agents-md-freshness.sh\ncheck-docs-templates.sh");

        Assert.All(_check.Evaluate(repo), c => Assert.True(c.Passed, c.Name));
    }

    [Fact]
    public void Evaluate_WithAzurePipelineWithoutChecks_OnlyPassesPipelineExists()
    {
        var repo = new FakeRepo().With("azure-pipelines.yml", "steps: []");

        var result = _check.Evaluate(repo);

        Assert.True(Sample.Passed(result, "A CI pipeline"));
        Assert.False(Sample.Passed(result, "CI fails"));
    }
}

public class SensitiveDataCheckTests
{
    private readonly SensitiveDataCheck _check = new();

    [Theory]
    [InlineData(".env")]
    [InlineData("src/Api/.env.production")]
    [InlineData("certs/site.pfx")]
    public void Evaluate_WithSecretFileCommitted_FailsAndNamesIt(string path)
    {
        var criterion = _check.Evaluate(new FakeRepo().With(path, "x")).First();

        Assert.False(criterion.Passed);
        Assert.Contains(path, criterion.Advice);
    }

    [Fact]
    public void Evaluate_WithEnvExample_Passes()
    {
        Assert.True(Sample.Passed(_check.Evaluate(new FakeRepo().With(".env.example", "KEY=<value>")), "No secret files"));
    }

    [Theory]
    [InlineData("Server=db;Password=Tr0ub4dor;", false)]
    [InlineData("AccountKey=abcd1234efgh", false)]
    [InlineData("Server=db;Password=<from-key-vault>;", true)]
    [InlineData("Server=db;Password=${DB_PASSWORD};", true)]
    public void Evaluate_ScansConfigFilesForLiteralSecrets(string connection, bool expectedPass)
    {
        var repo = new FakeRepo().With("src/Api/appsettings.json", $"{{ \"ConnectionStrings\": {{ \"Db\": \"{connection}\" }} }}");

        Assert.Equal(expectedPass, Sample.Passed(_check.Evaluate(repo), "No passwords"));
    }

    [Theory]
    [InlineData(".env\nbin/", true)]
    [InlineData(".env.*", true)]
    [InlineData("bin/\nobj/", false)]
    public void Evaluate_ChecksGitignoreForEnvFiles(string gitignore, bool expected)
    {
        var repo = new FakeRepo().With(".gitignore", gitignore);

        Assert.Equal(expected, Sample.Passed(_check.Evaluate(repo), ".gitignore"));
    }
}

public class EvaluationSetCheckTests
{
    private readonly EvaluationSetCheck _check = new();

    [Theory]
    [InlineData(9, false, false)]
    [InlineData(10, true, false)]
    [InlineData(30, true, true)]
    public void Evaluate_CountsQuestions(int count, bool ten, bool thirty)
    {
        var result = _check.Evaluate(new FakeRepo().With("evals/questions.yaml", Sample.Questions(count)));

        Assert.Equal(ten, Sample.Passed(result, "At least 10"));
        Assert.Equal(thirty, Sample.Passed(result, "At least 30"));
    }
}

public class AgentSettingsCheckTests
{
    [Fact]
    public void Evaluate_WithHook_PassesBoth()
    {
        var repo = new FakeRepo().With(".claude/settings.json", "{ \"hooks\": { \"PreToolUse\": [] } }");

        Assert.All(new AgentSettingsCheck().Evaluate(repo), c => Assert.True(c.Passed, c.Name));
    }

    [Fact]
    public void Evaluate_WithoutSettings_FailsBoth()
    {
        Assert.All(new AgentSettingsCheck().Evaluate(new FakeRepo()), c => Assert.False(c.Passed));
    }
}
