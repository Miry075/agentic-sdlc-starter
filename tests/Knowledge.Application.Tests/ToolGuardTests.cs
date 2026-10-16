using Knowledge.Core;

namespace Knowledge.Application.Tests;

public class ToolGuardTests
{
    private static readonly CallerContext Developer = Build.Caller("developers");

    private static ToolInvocation Invocation(string tool = "echo", string phase = "build", string? state = null) =>
        new(Developer, tool, phase, "L1", state);

    [Fact]
    public async Task RunAsync_WhenAllowed_ReturnsRedactedContentAndAuditsAllow()
    {
        var audit = new InMemoryAuditLog();

        var result = await Build.Guard(audit).RunAsync(
            Invocation(), _ => Task.FromResult("Ask jane@example.test"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("Ask [EMAIL]", result.Text);
        var audited = Assert.Single(audit.Events);
        Assert.Equal(AuditDecision.Allow, audited.Decision);
        Assert.Equal(1, audited.Redactions["EMAIL"]);
        Assert.Equal("Ask [EMAIL]".Length, audited.Chars);
        Assert.Equal("user-1", audited.UserId);
        Assert.Equal("build", audited.Phase);
    }

    [Fact]
    public async Task RunAsync_WhenDenied_DoesNotReadTheSourceAndAuditsDeny()
    {
        var audit = new InMemoryAuditLog();
        var fetched = false;

        var result = await Build.Guard(audit).RunAsync(
            Invocation(phase: "requirements", tool: "search_adrs"),
            _ =>
            {
                fetched = true;
                return Task.FromResult("secret");
            },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("'search_adrs' is not available in phase 'requirements'.", result.Text);
        Assert.False(fetched);
        var audited = Assert.Single(audit.Events);
        Assert.Equal(AuditDecision.Deny, audited.Decision);
        Assert.Equal(result.Text, audited.Reason);
    }

    [Fact]
    public async Task RunAsync_WhenArgumentsAreInvalid_ReturnsTheMessageAndAuditsError()
    {
        var audit = new InMemoryAuditLog();

        var result = await Build.Guard(audit).RunAsync(
            Invocation(), _ => throw new ToolArgumentException("Argument 'term' is required."), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Argument 'term' is required.", result.Text);
        Assert.Equal(AuditDecision.Error, Assert.Single(audit.Events).Decision);
    }

    [Fact]
    public async Task RunAsync_WhenSourceIsUnavailable_AsksTheAgentToRetryLater()
    {
        var audit = new InMemoryAuditLog();

        var result = await Build.Guard(audit).RunAsync(
            Invocation(), _ => throw new SourceUnavailableException("azure-devops"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Source temporarily unavailable; retry later.", result.Text);
    }

    [Fact]
    public async Task RunAsync_WhenToolFailsUnexpectedly_HidesTheDetailsFromTheAgent()
    {
        var audit = new InMemoryAuditLog();

        var result = await Build.Guard(audit).RunAsync(
            Invocation(), _ => throw new InvalidOperationException("connection string Server=prod"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("The tool failed unexpectedly.", result.Text);
        Assert.Equal("unexpected error", Assert.Single(audit.Events).Reason);
    }

    [Fact]
    public async Task RunAsync_WhenResultIsTooLong_TruncatesAfterRedaction()
    {
        var audit = new InMemoryAuditLog();
        var content = "jane@example.test " + new string('x', 200);

        var result = await Build.Guard(audit, maxChars: 50).RunAsync(
            Invocation(), _ => Task.FromResult(content), CancellationToken.None);

        Assert.StartsWith("[EMAIL] xxx", result.Text);
        Assert.EndsWith("[truncated: result exceeded the size limit]", result.Text);
        Assert.DoesNotContain("jane", result.Text);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_PropagatesCancellation()
    {
        var audit = new InMemoryAuditLog();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Build.Guard(audit).RunAsync(
            Invocation(), _ => throw new OperationCanceledException(), CancellationToken.None));
    }
}
