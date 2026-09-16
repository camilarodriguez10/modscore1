using System.Security.Claims;
using Modernization.ChannelApi.Auth;

namespace Modernization.Api.Tests;

/// <summary>
/// PA-03: si el IdP no puede emitir el claim de alcance, la plataforma mapea grupos.
/// </summary>
/// <remarks>
/// La regla es que el claim propio manda. Sumar los grupos a un alcance ya emitido
/// podria ampliarlo sin que nadie lo haya decidido, y ampliar el alcance es
/// exactamente lo que no puede pasar por accidente (H-02).
/// </remarks>
public sealed class ScopeFromGroupsTests
{
    private static readonly GroupScopeMapping Mapping = new(
        "groups",
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["modscore-lob-individual"] = "lob:Individual",
            ["modscore-chile"] = "country:Chile",
            ["modscore-todo"] = "*",
        });

    [Fact]
    public void WithoutAMappingOnlyTheClaimCounts()
    {
        var permissions = new ChannelPermissions(ChannelPermissions.DefaultScopeClaimType);
        var user = User(("groups", "modscore-lob-individual"));

        Assert.True(permissions.ScopeOf(user).IsEmpty);
    }

    [Fact]
    public void AGroupGrantsItsScopeWhenTheClaimIsMissing()
    {
        var permissions = Permissions();
        var user = User(("groups", "modscore-lob-individual"), ("groups", "modscore-chile"));

        var scope = permissions.ScopeOf(user);

        Assert.False(scope.IsEmpty);
        Assert.False(scope.IsFull);
        Assert.Contains("lob:Individual", scope.ToHeaderValue(), StringComparison.Ordinal);
        Assert.Contains("country:Chile", scope.ToHeaderValue(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheClaimWinsOverTheGroups()
    {
        var permissions = Permissions();
        var user = User(
            (ChannelPermissions.DefaultScopeClaimType, "lob:Retirement"),
            ("groups", "modscore-todo"));

        var scope = permissions.ScopeOf(user);

        Assert.False(scope.IsFull);
        Assert.Contains("lob:Retirement", scope.ToHeaderValue(), StringComparison.Ordinal);
    }

    [Fact]
    public void AGroupOutsideTheMapGrantsNothing()
    {
        var permissions = Permissions();
        var user = User(("groups", "un-grupo-cualquiera-de-la-empresa"));

        Assert.True(permissions.ScopeOf(user).IsEmpty);
    }

    [Fact]
    public void TheMapCanGrantTheWholePortfolio()
    {
        var permissions = Permissions();
        var user = User(("groups", "modscore-todo"));

        Assert.True(permissions.ScopeOf(user).IsFull);
    }

    // -----------------------------------------------------------------------

    private static ChannelPermissions Permissions()
        => new(ChannelPermissions.DefaultScopeClaimType, null, Mapping);

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity([.. claims.Select(c => new Claim(c.Type, c.Value))], "test"));
}
