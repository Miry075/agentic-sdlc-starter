using Knowledge.Core;

namespace Knowledge.Policy;

/// <summary>
/// allowed = tool in role ceiling AND phase allowed by work-item state AND tool in declared phase.
/// The declared phase can only narrow access.
/// </summary>
public sealed class PhasePolicy : IPhasePolicy
{
    private readonly PolicyDocument _policy;
    private readonly IToolPinStatus _pins;
    private readonly HashSet<string> _toolsInAnyPhase;

    public PhasePolicy(PolicyDocument policy, IToolPinStatus pins)
    {
        _policy = policy;
        _pins = pins;
        _toolsInAnyPhase = policy.Phases.Values.SelectMany(t => t).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> Phases => _policy.Phases.Keys.ToList();

    public PolicyDecision Evaluate(AccessRequest request)
    {
        var phase = request.DeclaredPhase;

        if (string.IsNullOrWhiteSpace(phase))
        {
            return PolicyDecision.Deny("The 'phase' argument is required.");
        }

        if (!_policy.Phases.TryGetValue(phase, out var phaseTools))
        {
            return PolicyDecision.Deny($"Unknown phase '{phase}'.");
        }

        if (!_policy.Tools.TryGetValue(request.Tool, out var tool))
        {
            return PolicyDecision.Deny($"Unknown tool '{request.Tool}'.");
        }

        var stateDenial = CheckWorkItemState(tool, phase, request.WorkItemState);
        if (stateDenial is not null)
        {
            return stateDenial;
        }

        if (!AllowedByRoles(request.Caller, request.Tool))
        {
            return PolicyDecision.Deny($"Your role does not allow '{request.Tool}'.");
        }

        if (!phaseTools.Contains(request.Tool))
        {
            return PolicyDecision.Deny($"'{request.Tool}' is not available in phase '{phase}'.");
        }

        if (tool.Upstream is not null && !_pins.IsApproved(tool.Name))
        {
            return PolicyDecision.Deny(
                $"'{request.Tool}' changed since it was approved; ask an administrator to re-approve it.");
        }

        return PolicyDecision.Allow();
    }

    public IReadOnlySet<string> ToolsVisibleTo(CallerContext caller) =>
        _toolsInAnyPhase
            .Where(name => AllowedByRoles(caller, name))
            .Where(name => _policy.Tools[name].Upstream is null || _pins.IsApproved(name))
            .ToHashSet(StringComparer.Ordinal);

    private PolicyDecision? CheckWorkItemState(ToolDefinition tool, string phase, string? state)
    {
        if (!tool.NeedsWorkItemState)
        {
            return null;
        }

        if (state is null)
        {
            return PolicyDecision.Deny("The work item state could not be determined.");
        }

        if (!_policy.WorkItemStates.TryGetValue(state, out var allowedPhases))
        {
            return PolicyDecision.Deny($"Work item state '{state}' is not recognised.");
        }

        return allowedPhases.Contains(phase)
            ? null
            : PolicyDecision.Deny($"Work item in state '{state}' does not allow phase '{phase}'.");
    }

    private bool AllowedByRoles(CallerContext caller, string tool) =>
        caller.Roles.Any(role => _policy.Roles.TryGetValue(role, out var ceiling) && ceiling.Allows(tool));
}
