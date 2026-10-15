namespace Knowledge.Policy;

// YamlDotNet needs mutable types. They never leave this assembly: PolicyLoader turns them into the immutable model.

internal sealed class PolicyDto
{
    public string? DefaultDecision { get; set; }
    public Dictionary<string, ToolDto?>? Tools { get; set; }
    public Dictionary<string, PhaseDto?>? Phases { get; set; }
    public Dictionary<string, List<string>?>? WorkItemStates { get; set; }
    public Dictionary<string, object?>? Roles { get; set; }
    public Dictionary<string, UpstreamDto?>? Upstreams { get; set; }
    public List<string>? WorkItemFieldAllowlist { get; set; }
}

internal sealed class ToolDto
{
    public string? Layer { get; set; }
    public string? Description { get; set; }
    public bool NeedsWorkItemState { get; set; }
}

internal sealed class PhaseDto
{
    public List<string>? Tools { get; set; }
}

internal sealed class UpstreamDto
{
    public string? Layer { get; set; }
    public Dictionary<string, UpstreamToolDto?>? Tools { get; set; }
    public UpstreamResourcesDto? Resources { get; set; }
}

internal sealed class UpstreamToolDto
{
    public string? Pin { get; set; }
}

internal sealed class UpstreamResourcesDto
{
    public List<string>? Allow { get; set; }
    public List<string>? Phases { get; set; }
}
