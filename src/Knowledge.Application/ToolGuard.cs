using System.Diagnostics;
using Knowledge.Core;
using Microsoft.Extensions.Logging;

namespace Knowledge.Application;

public sealed class ToolGuardOptions
{
    /// <summary>Longest result returned to an agent, in characters. Longer results are truncated after redaction.</summary>
    public int MaxResultChars { get; init; } = 64 * 1024;
}

/// <summary>
/// The single place where source content may leave the gateway:
/// policy, then fetch, then redact, then audit. Every call produces exactly one audit event.
/// </summary>
public sealed class ToolGuard(
    IPhasePolicy policy,
    IDataRedactor redactor,
    IAuditLog audit,
    TimeProvider clock,
    ToolGuardOptions options,
    ILogger<ToolGuard> logger) : IToolGuard
{
    private const string UnavailableMessage = "Source temporarily unavailable; retry later.";

    public async Task<ToolResult> RunAsync(
        ToolInvocation invocation,
        Func<CancellationToken, Task<string>> fetch,
        CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();

        var decision = policy.Evaluate(new AccessRequest(
            invocation.Caller, invocation.Tool, invocation.Phase, invocation.WorkItemState));

        if (!decision.Allowed)
        {
            Write(invocation, AuditDecision.Deny, decision.Reason, 0, NoRedactions, started);
            return ToolResult.Error(decision.Reason);
        }

        string raw;
        try
        {
            raw = await fetch(cancellationToken);
        }
        catch (ToolArgumentException ex)
        {
            Write(invocation, AuditDecision.Error, "invalid arguments", 0, NoRedactions, started);
            return ToolResult.Error(ex.Message);
        }
        catch (SourceUnavailableException ex)
        {
            logger.LogWarning(ex, "Source unavailable for tool {Tool}", invocation.Tool);
            Write(invocation, AuditDecision.Error, "source unavailable", 0, NoRedactions, started);
            return ToolResult.Error(UnavailableMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool {Tool} failed unexpectedly", invocation.Tool);
            Write(invocation, AuditDecision.Error, "unexpected error", 0, NoRedactions, started);
            return ToolResult.Error("The tool failed unexpectedly.");
        }

        var redacted = redactor.Redact(raw);
        var text = Truncate(redacted.Text);

        Write(invocation, AuditDecision.Allow, decision.Reason, text.Length, redacted.ByKind, started);
        return ToolResult.Ok(text);
    }

    private static readonly IReadOnlyDictionary<string, int> NoRedactions = new Dictionary<string, int>();

    private string Truncate(string text)
    {
        if (text.Length <= options.MaxResultChars)
        {
            return text;
        }

        return text[..options.MaxResultChars] + "\n\n[truncated: result exceeded the size limit]";
    }

    private void Write(
        ToolInvocation invocation,
        AuditDecision decision,
        string reason,
        int chars,
        IReadOnlyDictionary<string, int> redactions,
        long started)
    {
        audit.Write(new AuditEvent(
            decision,
            invocation.Caller.UserId,
            invocation.Caller.Roles.Order().ToList(),
            invocation.Caller.AuthMode,
            invocation.Phase,
            invocation.Tool,
            invocation.Layer,
            invocation.Upstream,
            invocation.WorkItemState,
            reason,
            chars,
            redactions,
            (long)clock.GetElapsedTime(started).TotalMilliseconds,
            Activity.Current?.TraceId.ToString()));
    }
}
