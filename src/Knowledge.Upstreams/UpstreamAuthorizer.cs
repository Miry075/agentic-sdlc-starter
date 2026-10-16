using Knowledge.Core;

namespace Knowledge.Upstreams;

/// <summary>
/// Produces the Authorization header the gateway presents to an upstream, using the gateway's own secret or
/// identity. The caller's token is never forwarded.
/// </summary>
public sealed class UpstreamAuthorizer(IUpstreamSecrets secrets, IServiceTokenProvider tokens)
{
    public async Task<string?> GetAuthorizationAsync(UpstreamAuthConfig auth, CancellationToken cancellationToken)
    {
        switch (auth.Mode)
        {
            case UpstreamAuthMode.None:
                return null;

            case UpstreamAuthMode.KeyVaultSecret:
                var secret = await secrets.GetSecretAsync(auth.Secret!, cancellationToken)
                    ?? throw new InvalidOperationException($"The secret '{auth.Secret}' for an upstream was not found.");
                return secret.Contains(' ') ? secret : "Bearer " + secret;

            case UpstreamAuthMode.ManagedIdentity:
                return "Bearer " + await tokens.GetTokenAsync(auth.Scope!, cancellationToken);

            default:
                throw new NotSupportedException($"Upstream auth mode {auth.Mode} is not supported.");
        }
    }
}

/// <summary>Adds the upstream's Authorization header to every request, fetching a fresh token each time.</summary>
public sealed class UpstreamAuthHandler(UpstreamAuthorizer authorizer, UpstreamAuthConfig auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var header = await authorizer.GetAuthorizationAsync(auth, cancellationToken);
        if (header is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", header);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
