namespace Knowledge.Policy;

public sealed record ToolDefinition(
    string Name,
    string Layer,
    bool NeedsWorkItemState,
    string? Upstream,
    string? Pin);

public sealed record UpstreamPolicy(
    string Name,
    string Layer,
    IReadOnlyDictionary<string, string?> ToolPins,
    IReadOnlyList<string> ResourceAllow,
    IReadOnlySet<string> ResourcePhases);

/// <summary>What a role may use. Roles are a ceiling, never a grant on their own.</summary>
public sealed class RoleCeiling
{
    private readonly IReadOnlySet<string>? _tools;

    private RoleCeiling(IReadOnlySet<string>? tools) => _tools = tools;

    public static RoleCeiling All { get; } = new(null);

    public static RoleCeiling Only(IEnumerable<string> tools) => new(tools.ToHashSet(StringComparer.Ordinal));

    public bool Allows(string tool) => _tools is null || _tools.Contains(tool);
}

/// <summary>A validated, immutable access policy. Build it with <see cref="PolicyLoader"/>.</summary>
public sealed class PolicyDocument
{
    public PolicyDocument(
        IReadOnlyDictionary<string, ToolDefinition> tools,
        IReadOnlyDictionary<string, IReadOnlySet<string>> phases,
        IReadOnlyDictionary<string, IReadOnlySet<string>> workItemStates,
        IReadOnlyDictionary<string, RoleCeiling> roles,
        IReadOnlyDictionary<string, UpstreamPolicy> upstreams,
        IReadOnlySet<string> workItemFieldAllowlist)
    {
        Tools = tools;
        Phases = phases;
        WorkItemStates = workItemStates;
        Roles = roles;
        Upstreams = upstreams;
        WorkItemFieldAllowlist = workItemFieldAllowlist;
    }

    /// <summary>Every tool the policy knows: native tools and declared upstream tools (qualified as "upstream.tool").</summary>
    public IReadOnlyDictionary<string, ToolDefinition> Tools { get; }

    /// <summary>Phase name to the tools available in that phase.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Phases { get; }

    /// <summary>Work-item state to the phases that state allows.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> WorkItemStates { get; }

    public IReadOnlyDictionary<string, RoleCeiling> Roles { get; }

    public IReadOnlyDictionary<string, UpstreamPolicy> Upstreams { get; }

    public IReadOnlySet<string> WorkItemFieldAllowlist { get; }
}

/// <summary>The policy file is not valid. Lists every problem found, not just the first.</summary>
public sealed class InvalidPolicyException(IReadOnlyList<string> errors)
    : Exception("Invalid policy: " + string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
