using AiReadiness.Scanner.Domain;
using AiReadiness.Scanner.Reporting;

namespace AiReadiness.Scanner.Tests;

public class OrgAssessmentTests
{
    private static RepoResult Result(string name, int score) =>
        new(name, new Assessment([new Criterion("A", "x", score, Passed: true, "a"), new Criterion("A", "y", 100 - score, Passed: false, "b")]));

    [Fact]
    public void Ranked_OrdersLowestScoreFirst_ThenByName()
    {
        var org = new OrgAssessment([Result("b", 50), Result("c", 20), Result("a", 50)]);

        Assert.Equal(["c", "a", "b"], org.Ranked.Select(r => r.Name));
    }

    [Fact]
    public void AverageScore_RoundsHalfUp_AndIsZeroWhenEmpty()
    {
        Assert.Equal(46, new OrgAssessment([Result("a", 45), Result("b", 46)]).AverageScore);
        Assert.Equal(0, new OrgAssessment([]).AverageScore);
    }

    [Fact]
    public void AnyBelow_ComparesAgainstTheLowestScore()
    {
        var org = new OrgAssessment([Result("a", 30), Result("b", 80)]);

        Assert.True(org.AnyBelow(31));
        Assert.False(org.AnyBelow(30));
    }
}

public class HtmlReportTests
{
    private static readonly DateOnly Day = new(2026, 10, 16);

    private static Assessment Sample() => new(
    [
        new Criterion("Area <one>", "Passed thing", 5, true, "x"),
        new Criterion("Area <one>", "Failed <b>thing</b>", 7, false, "Do \"this\" & that"),
    ]);

    [Fact]
    public void Render_EncodesEveryUserControlledValue()
    {
        var html = HtmlReport.Render(Sample(), "<script>alert(1)</script>", Day, null);

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<b>thing", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("Do &quot;this&quot; &amp; that", html);
    }

    [Fact]
    public void Render_ShowsScoreBandAndFailedActionsFirst()
    {
        var html = HtmlReport.Render(Sample(), "repo", Day, null);

        Assert.Contains("5<span>/12</span>", html);
        Assert.Contains("Not ready", html);
        Assert.Contains("<strong>+7</strong>", html);
        Assert.DoesNotContain("class=\"cta\"", html);
    }

    [Fact]
    public void Render_WithCallToAction_AddsLink()
    {
        var html = HtmlReport.Render(Sample(), "repo", Day, new CallToAction("Book <now>", new Uri("https://example.test/a?b=1&c=2")));

        Assert.Contains("href=\"https://example.test/a?b=1&amp;c=2\"", html);
        Assert.Contains("Book &lt;now&gt;", html);
    }

    [Fact]
    public void RenderOrg_ListsRepositoriesLowestFirst()
    {
        var low = new RepoResult("low-repo", new Assessment([new Criterion("A", "x", 10, false, "a")]));
        var high = new RepoResult("high-repo", new Assessment([new Criterion("A", "x", 10, true, "a")]));

        var html = HtmlReport.RenderOrg(new OrgAssessment([high, low]), Day, null);

        Assert.Contains("2 repositories", html);
        Assert.True(html.IndexOf("low-repo", StringComparison.Ordinal) < html.IndexOf("high-repo", StringComparison.Ordinal));
    }
}
