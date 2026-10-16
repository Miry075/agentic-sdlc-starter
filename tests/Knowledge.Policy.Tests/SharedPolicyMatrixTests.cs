using Knowledge.Policy;
using static Knowledge.Policy.Tests.TestSupport;

namespace Knowledge.Policy.Tests;

/// <summary>
/// Pins the shipped policy/phase-access.yaml: one case per phase-and-tool cell of the README matrix.
/// Changing the policy on purpose means changing this table in the same pull request.
/// </summary>
public class SharedPolicyMatrixTests
{
    private static readonly string[] AllTools =
    [
        "get_glossary_term", "search_adrs", "get_spec", "search_code_graph",
        "get_service_map", "search_product_docs", "get_work_item",
    ];

    private static readonly Dictionary<string, string[]> Expected = new()
    {
        ["requirements"] = ["get_glossary_term", "search_product_docs", "get_work_item"],
        ["design"] = ["get_glossary_term", "search_adrs", "get_service_map", "search_code_graph", "search_product_docs", "get_work_item"],
        ["build"] = ["get_glossary_term", "search_adrs", "get_spec", "search_code_graph", "get_service_map"],
        ["test"] = ["get_spec", "search_code_graph"],
        ["release"] = ["get_spec", "get_work_item", "search_adrs"],
    };

    // A work-item state that allows the phase, so only the phase-to-tool rule is under test.
    private static readonly Dictionary<string, string> StateFor = new()
    {
        ["requirements"] = "New",
        ["design"] = "Approved",
        ["build"] = "Active",
        ["test"] = "Resolved",
        ["release"] = "Resolved",
    };

    public static TheoryData<string, string> Cells()
    {
        var data = new TheoryData<string, string>();
        foreach (var phase in Expected.Keys)
        {
            foreach (var tool in AllTools)
            {
                data.Add(phase, tool);
            }
        }

        return data;
    }

    private static PhasePolicy LoadShared() =>
        new(PolicyLoader.LoadFromFile(FindRepoFile("policy/phase-access.yaml")), new FakePinStatus(true));

    [Fact]
    public void SharedPolicyFile_Loads()
    {
        var policy = PolicyLoader.LoadFromFile(FindRepoFile("policy/phase-access.yaml"));

        Assert.Equal(5, policy.Phases.Count);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Evaluate_ForDeveloper_AllowsToolOnlyWhenPhaseListsIt(string phase, string tool)
    {
        var decision = LoadShared().Evaluate(Request(Caller("developers"), tool, phase, StateFor[phase]));

        Assert.Equal(Expected[phase].Contains(tool), decision.Allowed);
    }

    [Fact]
    public void Evaluate_ForProductOwner_CannotReadCodeGraphEvenInDesign()
    {
        var decision = LoadShared().Evaluate(Request(Caller("product-owners"), "search_code_graph", "design", "Approved"));

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Evaluate_ForTester_CanReadSpecInTest()
    {
        var decision = LoadShared().Evaluate(Request(Caller("testers"), "get_spec", "test", "Resolved"));

        Assert.True(decision.Allowed);
    }
}
