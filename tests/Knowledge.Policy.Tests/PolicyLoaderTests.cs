using Knowledge.Policy;
using static Knowledge.Policy.Tests.TestSupport;

namespace Knowledge.Policy.Tests;

public class PolicyLoaderTests
{
    private static InvalidPolicyException Load(string yaml) =>
        Assert.Throws<InvalidPolicyException>(() => PolicyLoader.Parse(yaml));

    [Fact]
    public void Parse_WithValidPolicy_LoadsToolsPhasesAndUpstreams()
    {
        var policy = PolicyLoader.Parse(StandardYaml(ValidPin));

        Assert.True(policy.Tools["get_spec"].NeedsWorkItemState);
        Assert.Equal("graphify", policy.Tools["graphify.query_graph"].Upstream);
        Assert.Equal("L2", policy.Tools["graphify.query_graph"].Layer);
        Assert.Equal(ValidPin, policy.Upstreams["graphify"].ToolPins["query_graph"]);
        Assert.Contains("search_adrs", policy.Phases["design"]);
    }

    [Fact]
    public void Parse_WhenDefaultDecisionIsMissing_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("defaultDecision: deny", "");

        Assert.Contains("'defaultDecision' must be 'deny'.", Load(yaml).Errors);
    }

    [Fact]
    public void Parse_WhenDefaultDecisionIsAllow_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("defaultDecision: deny", "defaultDecision: allow");

        Assert.Contains("'defaultDecision' must be 'deny'.", Load(yaml).Errors);
    }

    [Fact]
    public void Parse_WhenPhaseReferencesUnknownTool_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("test:         { tools: [get_spec] }", "test:         { tools: [get_spec, search_wiki] }");

        Assert.Contains("Phase 'test' references unknown tool 'search_wiki'.", Load(yaml).Errors);
    }

    [Fact]
    public void Parse_WhenUpstreamToolInPhaseHasNoPin_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace($"{{ pin: \"{ValidPin}\" }}", "{}");

        var errors = Load(yaml).Errors;

        Assert.Contains(errors, e => e.Contains("graphify.query_graph") && e.Contains("no pin"));
    }

    [Fact]
    public void Parse_WhenUpstreamToolHasNoPinAndIsNotUsedByAnyPhase_Loads()
    {
        var yaml = """
            defaultDecision: deny
            tools:
              search_adrs: { layer: L1 }
            phases:
              design: { tools: [search_adrs] }
            upstreams:
              graphify:
                layer: L2
                tools:
                  query_graph: {}
            """;

        var policy = PolicyLoader.Parse(yaml);

        Assert.Null(policy.Tools["graphify.query_graph"].Pin);
    }

    [Fact]
    public void Parse_WhenPinIsMalformed_Throws()
    {
        var yaml = StandardYaml("sha256:3f9a");

        Assert.Contains(Load(yaml).Errors, e => e.Contains("'pin' must look like"));
    }

    [Fact]
    public void Parse_WhenStateReferencesUnknownPhase_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("Closed:   []", "Closed:   [archive]");

        Assert.Contains("Work item state 'Closed' references unknown phase 'archive'.", Load(yaml).Errors);
    }

    [Fact]
    public void Parse_WhenRoleReferencesUnknownTool_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("[get_glossary_term, get_work_item]", "[get_glossary_term, get_wiki]");

        Assert.Contains("Role 'product-owners' references unknown tool 'get_wiki'.", Load(yaml).Errors);
    }

    [Fact]
    public void Parse_WhenRoleIsNeitherStarNorList_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("developers: \"*\"", "developers: everything");

        Assert.Contains("Role 'developers' must be \"*\" or a list of tools.", Load(yaml).Errors);
    }

    [Fact]
    public void Parse_WhenNativeToolNameContainsDot_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("search_adrs:       { layer: L1 }", "search.adrs:       { layer: L1 }");

        Assert.Contains(Load(yaml).Errors, e => e.Contains("native tool names cannot contain '.'"));
    }

    [Fact]
    public void Parse_WhenSeveralThingsAreWrong_ReportsAllOfThem()
    {
        var yaml = StandardYaml(ValidPin)
            .Replace("defaultDecision: deny", "defaultDecision: allow")
            .Replace("Closed:   []", "Closed:   [archive]");

        Assert.Equal(2, Load(yaml).Errors.Count);
    }

    [Fact]
    public void Parse_WhenKeyIsMisspelled_Throws()
    {
        var yaml = StandardYaml(ValidPin).Replace("workItemStates:", "workItemState:");

        Assert.Contains(Load(yaml).Errors, e => e.Contains("not valid"));
    }

    [Fact]
    public void Parse_WhenFileIsEmpty_Throws()
    {
        Assert.Contains("The file is empty.", Load("").Errors);
    }

    private static string WithResources(string resourcesBlock) => StandardYaml(ValidPin) + "\n    resources:\n" + resourcesBlock;

    [Fact]
    public void Parse_WhenUpstreamAllowsResources_DefinesAReadResourceToolInTheListedPhasesOnly()
    {
        var policy = PolicyLoader.Parse(WithResources("      allow: [\"graphify://node/*\"]\n      phases: [design]"));

        var tool = policy.Tools["graphify.read_resource"];
        Assert.True(tool.Synthetic);
        Assert.Contains("graphify.read_resource", policy.Phases["design"]);
        Assert.DoesNotContain("graphify.read_resource", policy.Phases["build"]);
    }

    [Fact]
    public void Parse_WhenUpstreamHasNoResourceAllowList_DefinesNoReadResourceTool()
    {
        var policy = PolicyLoader.Parse(StandardYaml(ValidPin));

        Assert.False(policy.Tools.ContainsKey("graphify.read_resource"));
    }

    [Fact]
    public void Parse_WhenResourcePhasesHaveNoAllowList_Throws()
    {
        var yaml = WithResources("      phases: [design]");

        Assert.Contains(Load(yaml).Errors, e => e.Contains("'resources.phases' needs at least one entry"));
    }

    [Fact]
    public void Parse_WhenResourcePhaseIsUnknown_Throws()
    {
        var yaml = WithResources("      allow: [\"graphify://node/*\"]\n      phases: [deploy]");

        Assert.Contains("Upstream 'graphify' lists unknown resource phase 'deploy'.", Load(yaml).Errors);
    }

    [Fact]
    public void Evaluate_ForReadResource_FollowsTheResourcePhases()
    {
        var policy = new PhasePolicy(
            PolicyLoader.Parse(WithResources("      allow: [\"graphify://node/*\"]\n      phases: [design]")),
            new FakePinStatus(true));

        Assert.True(policy.Evaluate(Request(Caller("developers"), "graphify.read_resource", "design")).Allowed);
        Assert.False(policy.Evaluate(Request(Caller("developers"), "graphify.read_resource", "build")).Allowed);
    }
}
