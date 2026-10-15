namespace Knowledge.Core;

public sealed record AccessRequest(
    CallerContext Caller,
    string Tool,              // native name, or "upstream.tool"
    string DeclaredPhase,
    string? WorkItemState);   // null when unknown or not applicable

public sealed record PolicyDecision(bool Allowed, string Reason)
{
    public static PolicyDecision Allow() => new(true, "allowed");

    public static PolicyDecision Deny(string reason) => new(false, reason);
}

public interface IPhasePolicy
{
    /// <summary>Names of the SDLC phases the policy knows.</summary>
    IReadOnlyCollection<string> Phases { get; }

    PolicyDecision Evaluate(AccessRequest request);

    /// <summary>Tools the caller may see in tools/list. A convenience only: every call is re-checked.</summary>
    IReadOnlySet<string> ToolsVisibleTo(CallerContext caller);
}

/// <summary>Tells the policy whether an upstream tool still matches the definition an administrator approved.</summary>
public interface IToolPinStatus
{
    bool IsApproved(string qualifiedToolName);
}
