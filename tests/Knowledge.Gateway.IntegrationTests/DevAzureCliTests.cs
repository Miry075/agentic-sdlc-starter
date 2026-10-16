using Knowledge.Core;
using Knowledge.Gateway.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Knowledge.Gateway.IntegrationTests;

public class DevAzureCliTests
{
    [Fact]
    public void DevMode_WithUseAzureCli_ReadsDownstreamSourcesWithTheDevelopersSignIn()
    {
        using var factory = new AzureCliGatewayFactory();

        var downstream = factory.Services.GetRequiredService<IDownstreamTokenProvider>();
        var service = factory.Services.GetRequiredService<IServiceTokenProvider>();

        Assert.IsType<AzureCliTokenProvider>(downstream);
        Assert.IsType<AzureCliTokenProvider>(service);
    }

    [Fact]
    public void DevMode_ByDefault_HasNoCloudTokens()
    {
        using var factory = new GatewayFactory();

        Assert.IsType<NoTokenProvider>(factory.Services.GetRequiredService<IDownstreamTokenProvider>());
    }
}
