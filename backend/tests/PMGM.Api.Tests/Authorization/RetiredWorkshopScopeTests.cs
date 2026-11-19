using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>#1035 T2c: sesión de usuarios cuyo Taller fue dado de baja (propuesta B, pendiente de ratificación del PO).</summary>
public sealed class RetiredWorkshopScopeTests
{
    private static readonly Guid Retired = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Active = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Ghost = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly IReadOnlyDictionary<Guid, WorkshopScopeState> RetiredSet = new Dictionary<Guid, WorkshopScopeState>
    { [Retired] = WorkshopScopeState.Retired, [Active] = WorkshopScopeState.Active };

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
        Assert.Equal([Retired.ToString()], result.FindAll(RetiredWorkshopScope.RetiredOrg).Select(x => x.Value));   // consulta histórica propia
    }

    [Fact]
    public void Unknown_or_unusable_org_guid_never_counts_as_an_active_scope_and_cannot_block_retired_only()
    {
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin),
            (InstitutionalClaims.Organization, Retired.ToString()), (InstitutionalClaims.Organization, Ghost.ToString()));   // Ghost: no existe en BD
        var result = RetiredWorkshopScope.Apply(user, RetiredSet);
        Assert.True(result.HasClaim(RetiredWorkshopScope.Marker, "true"));
        Assert.Empty(result.FindAll(InstitutionalClaims.Organization)); Assert.Empty(result.FindAll(InstitutionalClaims.Role));
        var states = new Dictionary<Guid, WorkshopScopeState> { [Retired] = WorkshopScopeState.Retired, [Ghost] = WorkshopScopeState.Unusable };
        Assert.True(RetiredWorkshopScope.Apply(user, states).HasClaim(RetiredWorkshopScope.Marker, "true"));
    }

    [Fact]
    public void Incoming_marker_and_retired_org_claims_are_never_trusted()
    {
        var spoof = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin), (InstitutionalClaims.Organization, Active.ToString()),
            (RetiredWorkshopScope.Marker, "true"), (RetiredWorkshopScope.RetiredOrg, Active.ToString()));
        var result = RetiredWorkshopScope.Apply(spoof, RetiredSet);
        Assert.False(result.HasClaim(x => x.Type is RetiredWorkshopScope.Marker or RetiredWorkshopScope.RetiredOrg));
        Assert.True(result.HasClaim(InstitutionalClaims.Organization, Active.ToString()));   // lo operativo legítimo no se toca
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
        // El Taller activo conserva su ámbito; el de baja solo queda como consulta histórica propia (no operativa).
        Assert.False(result.HasClaim(InstitutionalClaims.Organization, Retired.ToString()));
        Assert.Equal([Retired.ToString()], result.FindAll(RetiredWorkshopScope.RetiredOrg).Select(x => x.Value));
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

    [Fact]
    public void Unverifiable_org_scopes_are_always_dropped_and_leave_no_operational_claims_without_valid_scope_or_order()
    {
        // Sin ningún Taller de baja: un pmgm_org inexistente NO se conserva y, sin ámbito válido ni Orden, tampoco cargos ni ámbitos.
        var ghostOnly = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin), (InstitutionalClaims.Organization, Ghost.ToString()));
        var result = RetiredWorkshopScope.Apply(ghostOnly, RetiredSet);
        Assert.Empty(result.FindAll(InstitutionalClaims.Organization)); Assert.Empty(result.FindAll(InstitutionalClaims.Role));
        Assert.False(result.HasClaim(RetiredWorkshopScope.Marker, "true"));   // no es un Taller de baja: solo no tiene ámbito
        Assert.Equal("u1", result.FindFirst("sub")!.Value);
        // Taller activo + GUID fantasma: conserva el activo y descarta el fantasma.
        var mixed = RetiredWorkshopScope.Apply(User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin),
            (InstitutionalClaims.Organization, Active.ToString()), (InstitutionalClaims.Organization, Ghost.ToString())), RetiredSet);
        Assert.Equal([Active.ToString()], mixed.FindAll(InstitutionalClaims.Organization).Select(x => x.Value));
        Assert.True(mixed.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin));
        // Orden con org inválida: conserva Orden y cargos, descarta la org.
        var order = RetiredWorkshopScope.Apply(User((InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin), (InstitutionalClaims.Scope, "order"),
            (InstitutionalClaims.Organization, Ghost.ToString())), RetiredSet);
        Assert.True(order.HasClaim(InstitutionalClaims.Scope, "order")); Assert.Empty(order.FindAll(InstitutionalClaims.Organization));
        // El Máster no pierde sus cargos por una org obsoleta.
        var master = RetiredWorkshopScope.Apply(User((InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin), (InstitutionalClaims.Organization, Ghost.ToString())), RetiredSet);
        Assert.True(master.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin));
    }

    [Theory]
    [InlineData("workshop", "active", WorkshopScopeState.Active)]
    [InlineData("grand_lodge", "active", WorkshopScopeState.Active)]
    [InlineData("support", "active", WorkshopScopeState.Active)]
    [InlineData("workshop", "retired", WorkshopScopeState.Retired)]
    [InlineData("workshop", "weird", WorkshopScopeState.Unusable)]
    [InlineData("workshop", null, WorkshopScopeState.Unusable)]
    [InlineData("other_type", "active", WorkshopScopeState.Unusable)]
    [InlineData("grand_lodge", "retired", WorkshopScopeState.Unusable)]
    [InlineData(null, "active", WorkshopScopeState.Unusable)]
    public void Scope_type_is_validated_positively_not_inferred_as_active(string? type, string? status, WorkshopScopeState expected)
        => Assert.Equal(expected, RetiredWorkshopScopeMiddleware.Classify(type, status));

    [Fact]
    public void Platform_role_is_flat_across_workshops_so_retirement_never_grants_but_cannot_attribute_a_role_to_its_origin_workshop()
    {
        // Documenta el límite del modelo (claims sin vínculo cargo↔Taller): el cargo se conserva para el Taller activo; el Taller de baja pierde su ámbito.
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.TallerTesoreria), (InstitutionalClaims.Organization, Retired.ToString()), (InstitutionalClaims.Organization, Active.ToString()));
        var result = RetiredWorkshopScope.Apply(user, RetiredSet);
        Assert.True(result.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.TallerTesoreria));
        Assert.False(new InstitutionalAccessService().CanReadOrganization(result, Retired));   // ya no abre datos del Taller de baja
        Assert.True(new InstitutionalAccessService().CanReadOrganization(result, Active));
    }

    [Fact]
    public void Remove_forged_claims_only_drops_the_middleware_claims_and_keeps_everything_else()
    {
        var user = User((InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin), (InstitutionalClaims.Organization, Active.ToString()),
            (RetiredWorkshopScope.Marker, "true"), (RetiredWorkshopScope.RetiredOrg, Retired.ToString()));
        var result = RetiredWorkshopScope.RemoveForgedClaims(user);
        Assert.False(result.HasClaim(x => x.Type is RetiredWorkshopScope.Marker or RetiredWorkshopScope.RetiredOrg));
        Assert.True(result.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin));
        Assert.True(result.HasClaim(InstitutionalClaims.Organization, Active.ToString()));
        Assert.True(result.Identity!.IsAuthenticated);
    }
}
