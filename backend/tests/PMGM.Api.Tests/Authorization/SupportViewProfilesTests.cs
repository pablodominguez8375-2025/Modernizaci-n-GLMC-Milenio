using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class SupportViewProfilesTests
{
    [Fact]
    public void Catalog_has_unique_keys_and_never_a_privileged_role()
    {
        Assert.Equal(SupportViewProfiles.All.Count, SupportViewProfiles.All.Select(p => p.Key).Distinct().Count());
        Assert.All(SupportViewProfiles.All, p => Assert.DoesNotContain(p.Roles, r => SupportViewAsClaims.PrivilegedRoles.Contains(r)));
        Assert.All(SupportViewProfiles.All, p => Assert.False(SupportViewAsClaims.IsPrivileged(SupportViewProfiles.Claims(p, null))));
    }

    [Fact]
    public void Workshop_profiles_are_bound_to_the_support_workshop_and_order_profiles_to_the_order_scope()
    {
        var org = Guid.NewGuid();
        var tesorero = SupportViewProfiles.Find("tesorero")!;
        Assert.Equal([org.ToString()], SupportViewProfiles.Claims(tesorero, org).Organizations);
        Assert.Null(SupportViewProfiles.Claims(tesorero, org).Scope);
        Assert.True(SupportViewProfiles.AllowsRoute(tesorero, "/api/gestion-logial/tesoreria"));
        var gs = SupportViewProfiles.Find("gran-secretario")!;
        Assert.Equal("order", SupportViewProfiles.Claims(gs, org).Scope);
        Assert.Empty(SupportViewProfiles.Claims(gs, org).Organizations);
        // El Máster ve todo (PO 27-10-2026): lecturas de Gran Logia sí; descargas y exportaciones no.
        Assert.True(SupportViewProfiles.AllowsRoute(gs, "/api/gran-secretaria/algo"));
        Assert.True(SupportViewProfiles.AllowsRoute(gs, "/api/session/me"));
        Assert.False(SupportViewProfiles.AllowsRoute(gs, "/api/regimen-interior/asambleas/{assemblyId:guid}/padron-oficial/export.pdf"));
        Assert.False(SupportViewProfiles.AllowsRoute(gs, "/api/regimen-interior/asambleas/{assemblyId:guid}/asambleistas-preliminar.csv"));
        Assert.False(SupportViewProfiles.AllowsRoute(tesorero, "/api/candidate-form/{captureRevision:int}/export.docx"));
        Assert.Null(SupportViewProfiles.Find("platform_superadmin"));
        Assert.Equal("Tesorero · Taller 00", SupportViewProfiles.DisplayName(tesorero));
    }
}
