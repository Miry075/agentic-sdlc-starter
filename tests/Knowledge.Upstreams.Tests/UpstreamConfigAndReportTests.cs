namespace Knowledge.Upstreams.Tests;

public class UpstreamConfigValidatorTests
{
    private static UpstreamConfig Stdio(string name = "graphify") =>
        new() { Name = name, Transport = UpstreamTransport.Stdio, Command = "python" };

    private static UpstreamConfig Http(string url, UpstreamAuthConfig? auth = null) => new()
    {
        Name = "atlassian",
        Transport = UpstreamTransport.Http,
        Url = url,
        Auth = auth ?? new UpstreamAuthConfig { Mode = UpstreamAuthMode.KeyVaultSecret, Secret = "atlassian-token" },
    };

    [Fact]
    public void Validate_WithValidStdioAndHttpUpstreams_ReturnsNoErrors()
    {
        var errors = UpstreamConfigValidator.Validate([Stdio(), Http("https://mcp.example.test")]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithStdioAndNoCommand_Fails()
    {
        var config = Stdio();
        config.Command = null;

        Assert.Contains(UpstreamConfigValidator.Validate([config]), e => e.Contains("'Command' is required"));
    }

    [Fact]
    public void Validate_WithPlainHttpToARemoteHost_Fails()
    {
        Assert.Contains(UpstreamConfigValidator.Validate([Http("http://mcp.example.test")]), e => e.Contains("must use https"));
    }

    [Fact]
    public void Validate_WithPlainHttpToLocalhost_IsAccepted()
    {
        Assert.Empty(UpstreamConfigValidator.Validate([Http("http://localhost:8080")]));
    }

    [Fact]
    public void Validate_WithDuplicateNames_Fails()
    {
        Assert.Contains(UpstreamConfigValidator.Validate([Stdio(), Stdio()]), e => e.Contains("used twice"));
    }

    [Fact]
    public void Validate_WithDotInName_Fails()
    {
        Assert.Contains(UpstreamConfigValidator.Validate([Stdio("my.graph")]), e => e.Contains("cannot contain '.'"));
    }

    [Fact]
    public void Validate_WithOboAuth_ExplainsItIsNotAvailableYet()
    {
        var config = Http("https://mcp.example.test", new UpstreamAuthConfig { Mode = UpstreamAuthMode.Obo, Scope = "api://x/.default" });

        Assert.Contains(UpstreamConfigValidator.Validate([config]), e => e.Contains("planned for v0.2"));
    }

    [Fact]
    public void Validate_WithKeyVaultSecretAndNoSecretName_Fails()
    {
        var config = Http("https://mcp.example.test", new UpstreamAuthConfig { Mode = UpstreamAuthMode.KeyVaultSecret });

        Assert.Contains(UpstreamConfigValidator.Validate([config]), e => e.Contains("'Secret' is required"));
    }

    [Fact]
    public void Validate_WithAuthOnAStdioUpstream_Fails()
    {
        var config = Stdio();
        config.Auth = new UpstreamAuthConfig { Mode = UpstreamAuthMode.ManagedIdentity, Scope = "api://x/.default" };

        Assert.Contains(UpstreamConfigValidator.Validate([config]), e => e.Contains("Http transport only"));
    }
}

public class UpstreamReportTests
{
    [Fact]
    public async Task FormatApproval_ListsPinsForDeclaredToolsAndNamesUndeclaredOnes()
    {
        var client = new FakeUpstreamClient();
        client.Tools.Add(Samples.QueryGraph());
        client.Tools.Add(Samples.DeleteNode());
        var connection = Samples.Connection(client, Samples.Policy(queryGraphPin: null).Upstreams["graphify"]);
        await connection.RefreshAsync(CancellationToken.None);

        var text = UpstreamReport.FormatApproval(connection);

        Assert.Contains($"query_graph: {{ pin: \"{PinCalculator.Compute(Samples.QueryGraph())}\" }}", text);
        Assert.Contains("not declared in the policy (not exposed): delete_node", text);
    }
}
