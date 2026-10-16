using Knowledge.Core;

namespace Knowledge.Gateway.Telemetry;

/// <summary>
/// One structured event per tool call, through ILogger (and so through OpenTelemetry to Azure Monitor when
/// it is configured). Metadata only: the event type has no field for content.
/// </summary>
public sealed class LoggerAuditLog(ILogger<LoggerAuditLog> logger) : IAuditLog
{
    public void Write(AuditEvent e)
    {
        logger.LogInformation(
            new EventId(1, "knowledge.access"),
            "knowledge.access {Decision} {UserId} {Roles} {AuthMode} {Phase} {Tool} {Layer} {Upstream} {WorkItemState} {Reason} {Chars} {Redactions} {DurationMs} {TraceId}",
            e.Decision.ToString().ToUpperInvariant(),
            e.UserId,
            string.Join(',', e.Roles),
            e.AuthMode,
            e.Phase,
            e.Tool,
            e.Layer,
            e.Upstream,
            e.WorkItemState,
            e.Reason,
            e.Chars,
            string.Join(',', e.Redactions.Select(r => $"{r.Key}={r.Value}")),
            e.DurationMs,
            e.TraceId);
    }
}
