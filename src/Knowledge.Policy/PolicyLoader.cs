using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Knowledge.Policy;

/// <summary>Reads <c>phase-access.yaml</c> and refuses to produce a policy that is incomplete or unsafe.</summary>
public static partial class PolicyLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>Name of the gateway-defined tool that reads an upstream's resources.</summary>
    public const string ReadResourceTool = "read_resource";

    [GeneratedRegex("^sha256:[0-9a-f]{64}$")]
    private static partial Regex PinFormat();

    public static PolicyDocument LoadFromFile(string path) => Parse(File.ReadAllText(path));

    public static PolicyDocument Parse(string yaml)
    {
        PolicyDto? dto;
        try
        {
            dto = Deserializer.Deserialize<PolicyDto>(yaml);
        }
        catch (YamlException ex)
        {
            throw new InvalidPolicyException([$"The file is not valid: {ex.Message}"]);
        }

        if (dto is null)
        {
            throw new InvalidPolicyException(["The file is empty."]);
        }

        return Build(dto);
    }

    private static PolicyDocument Build(PolicyDto dto)
    {
        var errors = new List<string>();

        if (!string.Equals(dto.DefaultDecision, "deny", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("'defaultDecision' must be 'deny'.");
        }

        var tools = new Dictionary<string, ToolDefinition>(StringComparer.Ordinal);
        AddNativeTools(dto, tools, errors);
        var upstreams = AddUpstreams(dto, tools, errors);

        var phases = BuildPhases(dto, tools, upstreams, errors);
        var states = BuildStates(dto, phases, errors);
        var roles = BuildRoles(dto, tools, errors);

        if (phases.Count == 0)
        {
            errors.Add("At least one phase is required.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidPolicyException(errors);
        }

        var allowlist = (dto.WorkItemFieldAllowlist ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new PolicyDocument(tools, phases, states, roles, upstreams, allowlist);
    }

    private static void AddNativeTools(PolicyDto dto, Dictionary<string, ToolDefinition> tools, List<string> errors)
    {
        foreach (var (name, tool) in dto.Tools ?? new())
        {
            if (name.Contains('.'))
            {
                errors.Add($"Tool '{name}': native tool names cannot contain '.'.");
            }

            if (string.IsNullOrWhiteSpace(tool?.Layer))
            {
                errors.Add($"Tool '{name}': 'layer' is required.");
                continue;
            }

            tools[name] = new ToolDefinition(name, tool.Layer, tool.NeedsWorkItemState, null, null);
        }
    }

    private static Dictionary<string, UpstreamPolicy> AddUpstreams(
        PolicyDto dto, Dictionary<string, ToolDefinition> tools, List<string> errors)
    {
        var upstreams = new Dictionary<string, UpstreamPolicy>(StringComparer.Ordinal);

        foreach (var (name, upstream) in dto.Upstreams ?? new())
        {
            if (name.Contains('.'))
            {
                errors.Add($"Upstream '{name}': names cannot contain '.'.");
            }

            if (string.IsNullOrWhiteSpace(upstream?.Layer))
            {
                errors.Add($"Upstream '{name}': 'layer' is required.");
                continue;
            }

            var pins = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (toolName, toolDto) in upstream.Tools ?? new())
            {
                var pin = toolDto?.Pin;
                if (pin is not null && !PinFormat().IsMatch(pin))
                {
                    errors.Add($"Upstream tool '{name}.{toolName}': 'pin' must look like 'sha256:<64 lowercase hex characters>'.");
                    pin = null;
                }

                var qualified = $"{name}.{toolName}";
                pins[toolName] = pin;
                tools[qualified] = new ToolDefinition(qualified, upstream.Layer, false, name, pin);
            }

            var allow = upstream.Resources?.Allow ?? [];
            var resourcePhases = (upstream.Resources?.Phases ?? []).ToHashSet(StringComparer.Ordinal);

            if (allow.Count > 0)
            {
                var readResource = $"{name}.{ReadResourceTool}";
                tools[readResource] = new ToolDefinition(readResource, upstream.Layer, false, name, null, Synthetic: true);
            }
            else if (resourcePhases.Count > 0)
            {
                errors.Add($"Upstream '{name}': 'resources.phases' needs at least one entry in 'resources.allow'.");
            }

            upstreams[name] = new UpstreamPolicy(name, upstream.Layer, pins, allow, resourcePhases);
        }

        return upstreams;
    }

    private static Dictionary<string, IReadOnlySet<string>> BuildPhases(
        PolicyDto dto,
        Dictionary<string, ToolDefinition> tools,
        Dictionary<string, UpstreamPolicy> upstreams,
        List<string> errors)
    {
        var phases = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var (phaseName, phase) in dto.Phases ?? new())
        {
            var phaseTools = new HashSet<string>(StringComparer.Ordinal);
            foreach (var toolName in phase?.Tools ?? [])
            {
                if (!tools.TryGetValue(toolName, out var definition))
                {
                    errors.Add($"Phase '{phaseName}' references unknown tool '{toolName}'.");
                    continue;
                }

                if (definition.Upstream is not null && definition.Pin is null && !definition.Synthetic)
                {
                    errors.Add($"Phase '{phaseName}' uses upstream tool '{toolName}', which has no pin. Approve it first.");
                    continue;
                }

                phaseTools.Add(toolName);
            }

            phases[phaseName] = phaseTools;
        }

        // Resources an upstream exposes are readable in the phases its policy lists, and only there.
        foreach (var upstream in upstreams.Values)
        {
            foreach (var phaseName in upstream.ResourcePhases)
            {
                if (!phases.TryGetValue(phaseName, out var phaseTools))
                {
                    errors.Add($"Upstream '{upstream.Name}' lists unknown resource phase '{phaseName}'.");
                    continue;
                }

                if (tools.ContainsKey($"{upstream.Name}.{ReadResourceTool}"))
                {
                    phaseTools.Add($"{upstream.Name}.{ReadResourceTool}");
                }
            }
        }

        return phases.ToDictionary(p => p.Key, p => (IReadOnlySet<string>)p.Value, StringComparer.Ordinal);
    }

    private static Dictionary<string, IReadOnlySet<string>> BuildStates(
        PolicyDto dto, Dictionary<string, IReadOnlySet<string>> phases, List<string> errors)
    {
        var states = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (stateName, statePhases) in dto.WorkItemStates ?? new())
        {
            foreach (var phase in statePhases ?? [])
            {
                if (!phases.ContainsKey(phase))
                {
                    errors.Add($"Work item state '{stateName}' references unknown phase '{phase}'.");
                }
            }

            states[stateName] = (statePhases ?? []).ToHashSet(StringComparer.Ordinal);
        }

        return states;
    }

    private static Dictionary<string, RoleCeiling> BuildRoles(
        PolicyDto dto, Dictionary<string, ToolDefinition> tools, List<string> errors)
    {
        var roles = new Dictionary<string, RoleCeiling>(StringComparer.Ordinal);

        foreach (var (roleName, value) in dto.Roles ?? new())
        {
            switch (value)
            {
                case string s when s == "*":
                    roles[roleName] = RoleCeiling.All;
                    break;

                case List<object> items:
                    var names = items.Select(i => i?.ToString() ?? string.Empty).ToList();
                    foreach (var tool in names.Where(n => !tools.ContainsKey(n)))
                    {
                        errors.Add($"Role '{roleName}' references unknown tool '{tool}'.");
                    }

                    roles[roleName] = RoleCeiling.Only(names);
                    break;

                default:
                    errors.Add($"Role '{roleName}' must be \"*\" or a list of tools.");
                    break;
            }
        }

        return roles;
    }
}
