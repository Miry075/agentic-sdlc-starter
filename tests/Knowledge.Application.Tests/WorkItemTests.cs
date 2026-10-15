using Knowledge.Application.WorkItems;
using Knowledge.Core;

namespace Knowledge.Application.Tests;

public class WorkItemFormatterTests
{
    private static readonly WorkItem Item = new(
        7, "Issue credit note", "Active", "Customers cancel invoices.", "1. Given an invoice ...",
        [
            new WorkItemComment(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), "Older"),
            new WorkItemComment(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero), "Newer"),
        ],
        ["Parent #3"]);

    private static string Format(params string[] fields) =>
        new WorkItemFormatter(fields.ToHashSet(StringComparer.OrdinalIgnoreCase)).ToMarkdown(Item);

    [Fact]
    public void ToMarkdown_WithEveryField_RendersAllSections()
    {
        var text = Format("id", "title", "state", "description", "acceptanceCriteria", "comments", "links");

        Assert.Contains("# #7 Issue credit note", text);
        Assert.Contains("**State:** Active", text);
        Assert.Contains("## Description", text);
        Assert.Contains("## Acceptance criteria", text);
        Assert.Contains("- Parent #3", text);
        Assert.Contains("## Comments (latest first)", text);
    }

    [Fact]
    public void ToMarkdown_OnlyIncludesAllowlistedFields()
    {
        var text = Format("id", "title");

        Assert.DoesNotContain("Customers cancel invoices.", text);
        Assert.DoesNotContain("State", text);
        Assert.DoesNotContain("Newer", text);
    }

    [Fact]
    public void ToMarkdown_ListsCommentsNewestFirst()
    {
        var text = Format("comments");

        Assert.True(text.IndexOf("Newer", StringComparison.Ordinal) < text.IndexOf("Older", StringComparison.Ordinal));
    }

    [Fact]
    public void ToMarkdown_KeepsOnlyTheLatestTwentyComments()
    {
        var comments = Enumerable.Range(1, 25)
            .Select(i => new WorkItemComment(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddDays(i), $"comment-{i:D2}"))
            .ToList();
        var item = Item with { Comments = comments };

        var text = new WorkItemFormatter(new HashSet<string> { "comments" }).ToMarkdown(item);

        Assert.Contains("comment-25", text);
        Assert.Contains("comment-06", text);
        Assert.DoesNotContain("comment-05", text);
    }
}

public class CachingWorkItemSourceTests
{
    private static readonly CallerContext Alice = new("alice", new HashSet<string> { "developers" }, "Test");
    private static readonly CallerContext Bob = new("bob", new HashSet<string> { "developers" }, "Test");

    [Fact]
    public async Task GetAsync_WithinTimeToLive_ReadsTheTrackerOnce()
    {
        var inner = new FakeWorkItems().With(Build.AWorkItem(7));
        var source = new CachingWorkItemSource(inner, new ManualClock(DateTimeOffset.UnixEpoch), TimeSpan.FromSeconds(30));

        await source.GetAsync(Alice, 7, CancellationToken.None);
        await source.GetAsync(Alice, 7, CancellationToken.None);

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task GetAsync_AfterTimeToLive_ReadsTheTrackerAgain()
    {
        var clock = new ManualClock(DateTimeOffset.UnixEpoch);
        var inner = new FakeWorkItems().With(Build.AWorkItem(7));
        var source = new CachingWorkItemSource(inner, clock, TimeSpan.FromSeconds(30));

        await source.GetAsync(Alice, 7, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(31));
        await source.GetAsync(Alice, 7, CancellationToken.None);

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task GetAsync_ForAnotherUser_DoesNotShareTheCachedItem()
    {
        var inner = new FakeWorkItems().With(Build.AWorkItem(7));
        var source = new CachingWorkItemSource(inner, new ManualClock(DateTimeOffset.UnixEpoch), TimeSpan.FromSeconds(30));

        await source.GetAsync(Alice, 7, CancellationToken.None);
        await source.GetAsync(Bob, 7, CancellationToken.None);

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task GetAsync_WhenItemDoesNotExist_ReturnsNull()
    {
        var source = new CachingWorkItemSource(new FakeWorkItems(), new ManualClock(DateTimeOffset.UnixEpoch), TimeSpan.FromSeconds(30));

        Assert.Null(await source.GetAsync(Alice, 99, CancellationToken.None));
    }
}
