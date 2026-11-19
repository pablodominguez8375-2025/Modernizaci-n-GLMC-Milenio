using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>#1035 T2c: sesión de usuarios cuyo Taller fue dado de baja (propuesta B, pendiente de ratificación del PO).</summary>
public sealed class RetiredWorkshopScopeTests
{
    private static readonly Guid Retired = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Active = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly IReadOnlySet<Guid> RetiredSet = new HashSet<Guid> { Retired };

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims)
    {
        var all = new List<Claim> { new("sub", "u1"), new("name", "Hermano QA") };
        all.AddRange(claims.Select(c => new Claim(c.Type, c.Value)));
        return new ClaimsPrincipal(new ClaimsIdentity(all, "test", "name", ClaimTypes.Role));
    }

    [Fact]
    public void Only_retired_workshop_scope_yields_session_without_operational_scope()
    {
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin), (InstitutionalClaims.Organization, Retired.ToString()));
        var result = RetiredWorkshopScope.Apply(user, RetiredSet);
        Assert.True(result.Identity!.IsAuthenticated);
        Assert.Equal("u1", result.FindFirst("sub")!.Value);
        Assert.Empty(result.FindAll(InstitutionalClaims.Role));
        Assert.Empty(result.FindAll(InstitutionalClaims.Organization));
        Assert.Empty(result.FindAll(InstitutionalClaims.Scope));
        Assert.True(result.HasClaim(RetiredWorkshopScope.Marker, "true"));
    }

    [Fact]
    public void Other_active_workshop_keeps_only_active_scopes_and_roles()
    {
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin),
            (InstitutionalClaims.Organization, Retired.ToString()), (InstitutionalClaims.Organization, Active.ToString()));
        var result = RetiredWorkshopScope.Apply(user, RetiredSet);
        Assert.Equal([Active.ToString()], result.FindAll(InstitutionalClaims.Organization).Select(x => x.Value));
        Assert.True(result.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin));
        Assert.False(result.HasClaim(RetiredWorkshopScope.Marker, "true"));
    }

    [Fact]
    public void Order_scope_user_loses_only_the_retired_workshop()
    {
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin), (InstitutionalClaims.Scope, "order"),
            (InstitutionalClaims.Organization, Retired.ToString()));
        var result = RetiredWorkshopScope.Apply(user, RetiredSet);
        Assert.Empty(result.FindAll(InstitutionalClaims.Organization));
        Assert.True(result.HasClaim(InstitutionalClaims.Scope, "order"));
        Assert.True(result.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin));
        Assert.False(result.HasClaim(RetiredWorkshopScope.Marker, "true"));
    }

    [Fact]
    public void Unreadable_org_claim_next_to_a_retired_one_is_not_an_active_scope()
    {
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin),
            (InstitutionalClaims.Organization, Retired.ToString()), (InstitutionalClaims.Organization, "no-es-guid"));
        var result = RetiredWorkshopScope.Apply(user, RetiredSet);
        Assert.True(result.HasClaim(RetiredWorkshopScope.Marker, "true"));
        Assert.Empty(result.FindAll(InstitutionalClaims.Role));
        Assert.Empty(result.FindAll(InstitutionalClaims.Organization));
    }

    [Fact]
    public void Principals_without_retired_scopes_or_unauthenticated_are_returned_unchanged()
    {
        var active = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin), (InstitutionalClaims.Organization, Active.ToString()));
        Assert.Same(active, RetiredWorkshopScope.Apply(active, RetiredSet));
        Assert.Same(active, RetiredWorkshopScope.Apply(active, new HashSet<Guid>()));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Same(anonymous, RetiredWorkshopScope.Apply(anonymous, RetiredSet));
        var noOrg = User((InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin));
        Assert.Same(noOrg, RetiredWorkshopScope.Apply(noOrg, RetiredSet));
    }

    [Fact]
    public void Retired_only_user_gets_no_workshop_capability_from_the_access_service()
    {
        var access = new InstitutionalAccessService();
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin), (InstitutionalClaims.Organization, Retired.ToString()));
        var before = access.CanReadOrganization(user, Retired);
        var after = RetiredWorkshopScope.Apply(user, RetiredSet);
        Assert.True(before);
        Assert.False(access.CanReadOrganization(after, Retired));
        Assert.False(access.HasRole(after, InstitutionalRoles.TallerAdmin));
        var profile = SessionProfileBuilder.Build(after, access);
        Assert.Equal("retired_workshop", profile.AccessScope);
        Assert.False(profile.Capabilities.CanManageLodgeOperations);
    }
}
