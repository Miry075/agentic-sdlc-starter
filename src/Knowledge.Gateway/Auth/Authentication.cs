using System.Security.Claims;
using System.Text.Encodings.Web;
using Knowledge.Core;
using Knowledge.Gateway.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Knowledge.Gateway.Auth;

/// <summary>Turns the authenticated principal into the caller the policy reasons about.</summary>
public sealed class CallerFactory(AuthOptions options)
{
    private const string ObjectIdLong = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public CallerContext Create(ClaimsPrincipal user)
    {
        if (options.Mode == AuthMode.Dev)
        {
            return new CallerContext(options.Dev.UserId, options.Dev.Roles.ToHashSet(StringComparer.Ordinal), "Dev");
        }

        var userId = user.FindFirstValue("oid") ?? user.FindFirstValue(ObjectIdLong) ?? user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("The token has no user identifier.");

        var roleMap = new Dictionary<string, string>(options.RoleMap, StringComparer.OrdinalIgnoreCase);
        var roles = user.FindAll("groups")
            .Select(claim => roleMap.GetValueOrDefault(claim.Value))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        // A token with no mapped role is valid but sees no tools: that is not an error.
        return new CallerContext(userId, roles, "Entra");
    }
}

/// <summary>Reads the current request's caller. Registered as a singleton: it only touches the request through the accessor.</summary>
public sealed class CallerResolver(IHttpContextAccessor http, CallerFactory factory)
{
    public CallerContext Current =>
        factory.Create(http.HttpContext?.User ?? throw new InvalidOperationException("There is no current request."));
}

/// <summary>Authenticates every request as the configured dev user. Never registered outside Dev mode.</summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dev";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new Claim("oid", "dev")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
