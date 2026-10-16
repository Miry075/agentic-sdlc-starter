using Knowledge.Core;
using static Knowledge.Policy.Tests.TestSupport;

namespace Knowledge.Policy.Tests;

public class PhasePolicyTests
{
    private static readonly CallerContext Developer = Caller("developers");

    [Fact]
    public void Evaluate_WhenEverythingAligns_Allows()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "get_spec", "build", "Active"));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Evaluate_WhenPhaseIsBlank_DeniesAndAsksForPhase()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "search_adrs", " "));

        Assert.False(decision.Allowed);
        Assert.Equal("The 'phase' argument is required.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenPhaseIsUnknown_Denies()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "search_adrs", "deploy"));

        Assert.Equal("Unknown phase 'deploy'.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenToolIsUnknown_Denies()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "drop_database", "build"));

        Assert.Equal("Unknown tool 'drop_database'.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenWorkItemStateDoesNotAllowPhase_Denies()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "get_work_item", "requirements", "Active"));

        Assert.False(decision.Allowed);
        Assert.Equal("Work item in state 'Active' does not allow phase 'requirements'.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenClosedWorkItem_DeniesEveryPhase()
    {
        var policy = StandardPhasePolicy();

        foreach (var phase in policy.Phases)
        {
            Assert.False(policy.Evaluate(Request(Developer, "get_work_item", phase, "Closed")).Allowed);
        }
    }

    [Fact]
    public void Evaluate_WhenToolNeedsStateAndStateIsUnknown_DeniesInsteadOfSkippingTheCheck()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "get_spec", "build", state: null));

        Assert.Equal("The work item state could not be determined.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenStateIsNotInPolicy_Denies()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "get_spec", "build", "Reopened"));

        Assert.Equal("Work item state 'Reopened' is not recognised.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenToolDoesNotNeedState_IgnoresTheState()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "search_adrs", "design", "Closed"));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Evaluate_WhenRoleCeilingExcludesTool_Denies()
    {
        var productOwner = Caller("product-owners");

        var decision = StandardPhasePolicy().Evaluate(Request(productOwner, "search_adrs", "design"));

        Assert.Equal("Your role does not allow 'search_adrs'.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenCallerHasNoMappedRole_Denies()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Caller("unknown-group"), "get_glossary_term", "build"));

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Evaluate_WhenCallerHasNoRoles_Denies()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Caller(), "get_glossary_term", "build"));

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Evaluate_WhenCallerHasSeveralRoles_UsesTheUnionOfTheirCeilings()
    {
        var caller = Caller("product-owners", "developers");

        var decision = StandardPhasePolicy().Evaluate(Request(caller, "search_adrs", "design"));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Evaluate_WhenPhaseDoesNotOfferTool_DeniesEvenForDevelopers()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "get_glossary_term", "test"));

        Assert.Equal("'get_glossary_term' is not available in phase 'test'.", decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenUpstreamPinNoLongerMatches_Denies()
    {
        var policy = StandardPhasePolicy(pinsApproved: false);

        var decision = policy.Evaluate(Request(Developer, "graphify.query_graph", "build"));

        Assert.Equal(
            "'graphify.query_graph' changed since it was approved; ask an administrator to re-approve it.",
            decision.Reason);
    }

    [Fact]
    public void Evaluate_WhenUpstreamToolIsApprovedAndInPhase_Allows()
    {
        var decision = StandardPhasePolicy().Evaluate(Request(Developer, "graphify.query_graph", "build"));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void ToolsVisibleTo_ForDeveloper_ListsEveryToolUsedByAtLeastOnePhase()
    {
        var visible = StandardPhasePolicy().ToolsVisibleTo(Developer);

        Assert.Equal(
            ["get_glossary_term", "get_spec", "get_work_item", "graphify.query_graph", "search_adrs"],
            visible.Order().ToArray());
    }

    [Fact]
    public void ToolsVisibleTo_ForProductOwner_IsLimitedToTheirCeiling()
    {
        var visible = StandardPhasePolicy().ToolsVisibleTo(Caller("product-owners"));

        Assert.Equal(["get_glossary_term", "get_work_item"], visible.Order().ToArray());
    }

    [Fact]
    public void ToolsVisibleTo_ForCallerWithoutMappedRole_IsEmpty()
    {
        var visible = StandardPhasePolicy().ToolsVisibleTo(Caller("nobody"));

        Assert.Empty(visible);
    }

    [Fact]
    public void ToolsVisibleTo_WhenUpstreamPinMismatches_HidesTheUpstreamTool()
    {
        var visible = StandardPhasePolicy(pinsApproved: false).ToolsVisibleTo(Developer);

        Assert.DoesNotContain("graphify.query_graph", visible);
    }
}
