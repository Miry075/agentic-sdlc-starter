using System.Security.Claims;
using Knowledge.Gateway.Auth;
using Knowledge.Gateway.Configuration;

namespace Knowledge.Gateway.IntegrationTests;

public class CallerFactoryTests
{
    private const string DevelopersGroup = "6f1c0000-0000-0000-0000-000000000001";
    private const string ProductOwnersGroup = "a2b90000-0000-0000-0000-000000000002";

    private static readonly AuthOptions Entra = new()
    {
        Mode = AuthMode.Entra,
        RoleMap = new Dictionary<string, string>
        {
            [DevelopersGroup] = "developers",
            [ProductOwnersGroup] = "product-owners",
        },
    };

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void Create_InEntraMode_MapsGroupObjectIdsToRoles()
    {
        var user = Principal(new Claim("oid", "user-1"), new Claim("groups", DevelopersGroup), new Claim("groups", ProductOwnersGroup));

        var caller = new CallerFactory(Entra).Create(user);

        Assert.Equal("user-1", caller.UserId);
        Assert.Equal("Entra", caller.AuthMode);
        Assert.Equal(["developers", "product-owners"], caller.Roles.Order().ToArray());
    }

    [Fact]
    public void Create_InEntraMode_IgnoresGroupsThatAreNotMapped()
    {
        var user = Principal(new Claim("oid", "user-1"), new Claim("groups", "11111111-1111-1111-1111-111111111111"));

        var caller = new CallerFactory(Entra).Create(user);

        Assert.Empty(caller.Roles);
    }

    [Fact]
    public void Create_InEntraMode_AcceptsTheLongFormOfTheObjectIdClaim()
    {
        var user = Principal(new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "user-2"));

        Assert.Equal("user-2", new CallerFactory(Entra).Create(user).UserId);
    }

    [Fact]
    public void Create_InEntraMode_WithoutAUserIdentifier_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new CallerFactory(Entra).Create(Principal()));
    }

    [Fact]
    public void Create_InDevMode_UsesTheConfiguredUserAndRoles_AndSaysSo()
    {
        var options = new AuthOptions { Mode = AuthMode.Dev, Dev = { UserId = "me@localhost", Roles = ["testers"] } };

        var caller = new CallerFactory(options).Create(Principal());

        Assert.Equal("me@localhost", caller.UserId);
        Assert.Equal("Dev", caller.AuthMode);
        Assert.Equal(["testers"], caller.Roles.ToArray());
    }
}
