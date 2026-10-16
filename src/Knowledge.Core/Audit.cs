namespace Knowledge.Core;

public enum AuditDecision
{
    Allow,
    Deny,
    Error,
}

/// <summary>Metadata about one tool call. Never contains source content.</summary>
public sealed record AuditEvent(
    AuditDecision Decision,
    string UserId,
    IReadOnlyCollection<string> Roles,
    string AuthMode,
    string Phase,
    string Tool,
    string Layer,
    string? Upstream,
    string? WorkItemState,
    string Reason,
    int Chars,
    IReadOnlyDictionary<string, int> Redactions,
    long DurationMs,
    string? TraceId);

public interface IAuditLog
{
    void Write(AuditEvent auditEvent);
}
