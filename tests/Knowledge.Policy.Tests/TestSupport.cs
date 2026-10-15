using Knowledge.Core;
using Knowledge.Policy;

namespace Knowledge.Policy.Tests;

internal static class TestSupport
{
    public static readonly string ValidPin = "sha256:" + new string('a', 64);

    /// <summary>A small policy that exercises every rule. Not the shared policy file.</summary>
    public static string StandardYaml(string pin) => $$"""
        defaultDecision: deny
        tools:
          get_glossary_term: { layer: L1 }
          search_adrs:       { layer: L1 }
          get_spec:          { layer: L1, needsWorkItemState: true }
          get_work_item:     { layer: L4, needsWorkItemState: true }
        phases:
          requirements: { tools: [get_glossary_term, get_work_item, graphify.query_graph] }
          design:       { tools: [get_glossary_term, search_adrs, get_work_item] }
          build:        { tools: [get_glossary_term, search_adrs, get_spec, graphify.query_graph] }
          test:         { tools: [get_spec] }
        workItemStates:
          New:      [requirements, design]
          Approved: [design, build]
          Active:   [build, test]
          Closed:   []
        roles:
          developers: "*"
          product-owners: [get_glossary_term, get_work_item]
        upstreams:
          graphify:
            layer: L2
            tools:
              query_graph: { pin: "{{pin}}" }
        """;

    public static PolicyDocument StandardPolicy() => PolicyLoader.Parse(StandardYaml(ValidPin));

    public static PhasePolicy StandardPhasePolicy(bool pinsApproved = true) =>
        new(StandardPolicy(), new FakePinStatus(pinsApproved));

    public static CallerContext Caller(params string[] roles) =>
        new("user-1", roles.ToHashSet(StringComparer.Ordinal), "Test");

    public static AccessRequest Request(CallerContext caller, string tool, string phase, string? state = null) =>
        new(caller, tool, phase, state);

    /// <summary>Walks up from the test binaries to find a file that lives in the repository.</summary>
    public static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}' above '{AppContext.BaseDirectory}'.");
    }
}

internal sealed class FakePinStatus(bool approved) : IToolPinStatus
{
    public bool IsApproved(string qualifiedToolName) => approved;
}
