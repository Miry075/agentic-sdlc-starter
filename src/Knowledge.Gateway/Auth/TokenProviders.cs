using Azure.Core;
using Azure.Identity;
using Knowledge.Core;
using Knowledge.Upstreams;
using Microsoft.Identity.Web;

namespace Knowledge.Gateway.Auth;

/// <summary>
/// Gets a token for a downstream API on behalf of the signed-in user (on-behalf-of). The incoming token is
/// exchanged, never forwarded. It resolves the token service per request, because that service is request-scoped.
/// </summary>
public sealed class EntraOboTokenProvider(IHttpContextAccessor http) : IDownstreamTokenProvider
{
    public async Task<string> GetTokenAsync(CallerContext caller, string scope, CancellationToken cancellationToken)
    {
        var context = http.HttpContext ?? throw new InvalidOperationException("There is no current request.");
        var tokens = context.RequestServices.GetRequiredService<ITokenAcquisition>();

        return await tokens.GetAccessTokenForUserAsync([scope], user: context.User);
    }
}

/// <summary>The gateway's own identity (managed identity in Azure, developer sign-in locally).</summary>
public sealed class ManagedIdentityTokenProvider : IServiceTokenProvider
{
    private readonly TokenCredential _credential = new DefaultAzureCredential();

    public async Task<string> GetTokenAsync(string scope, CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken);
        return token.Token;
    }
}

/// <summary>Used in Dev mode, where no cloud source is configured.</summary>
public sealed class NoTokenProvider : IDownstreamTokenProvider, IServiceTokenProvider
{
    public Task<string> GetTokenAsync(CallerContext caller, string scope, CancellationToken cancellationToken) => throw Unavailable();

    public Task<string> GetTokenAsync(string scope, CancellationToken cancellationToken) => throw Unavailable();

    private static NotSupportedException Unavailable() =>
        new("No token provider is configured. Dev mode serves local sample data only.");
}

/// <summary>
/// Upstream secrets come from configuration key <c>UpstreamSecrets:&lt;name&gt;</c>. In Azure, add the Key Vault
/// configuration provider so those keys are read from Key Vault; nothing here knows about Key Vault.
/// </summary>
public sealed class ConfigurationUpstreamSecrets(IConfiguration configuration) : IUpstreamSecrets
{
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(configuration[$"UpstreamSecrets:{name}"]);
}
