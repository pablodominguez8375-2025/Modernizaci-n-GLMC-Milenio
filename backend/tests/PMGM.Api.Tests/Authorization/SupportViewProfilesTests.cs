using System.Security.Claims;
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

    [Theory]
    [InlineData("gran-segundo-vigilante", InstitutionalRoles.GranSegundoVigilante, 1)]
    [InlineData("gran-primer-vigilante", InstitutionalRoles.GranPrimerVigilante, 2)]
    [InlineData("inmediato-ex-gran-maestro", InstitutionalRoles.InmediatoExGranMaestro, 3)]
    public void Order_docencia_profiles_keep_only_their_assigned_degree(string key, string role, int degree)
    {
        var profile = SupportViewProfiles.Find(key)!;
        Assert.NotNull(profile);
        Assert.True(profile.OrderScope);
        Assert.Equal(SupportViewProfiles.GrandLodge, profile.Group);
        Assert.Equal(new[] { role }, profile.Roles);
        var claims = SupportViewProfiles.Claims(profile, Guid.NewGuid());
        Assert.Equal("order", claims.Scope);
        Assert.Empty(claims.Organizations);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(InstitutionalClaims.Scope, claims.Scope!),
            new Claim(InstitutionalClaims.Role, claims.Roles.Single())
        }, "qa-synthetic"));
        var access = new InstitutionalAccessService();
        foreach (var testDegree in new[] { 1, 2, 3 })
            Assert.Equal(testDegree == degree, access.CanReadOrderLodgeInstructions(principal, testDegree));
        Assert.False(SupportViewProfiles.AllowsRoute(profile, "/api/reportes/export.pdf"));
    }

    [Fact]
    public void Department_head_reads_three_degrees_but_grand_hospitalaria_is_not_docencia()
    {
        var access = new InstitutionalAccessService();
        foreach (var (key, role, canRead) in new[]
        {
            ("jefatura-docencia", InstitutionalRoles.JefaturaDepartamentoDocencia, true),
            ("gran-hospitalaria", InstitutionalRoles.GranHospitalaria, false)
        })
        {
            var profile = SupportViewProfiles.Find(key)!;
            Assert.NotNull(profile);
            Assert.True(profile.OrderScope);
            Assert.Equal(new[] { role }, profile.Roles);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            }, "qa-synthetic"));
            foreach (var degree in new[] { 1, 2, 3 })
                Assert.Equal(canRead, access.CanReadOrderLodgeInstructions(principal, degree));
        }
    }

    [Fact]
    public void Immediate_past_venerable_stays_in_support_workshop_without_order_scope()
    {
        var org = Guid.NewGuid();
        var p = SupportViewProfiles.Find("inmediato-ex-venerable")!;
        Assert.NotNull(p);
        Assert.Equal(new[] { InstitutionalRoles.TallerInmediatoExVenerable }, p.Roles);
        var claims = SupportViewProfiles.Claims(p, org);
        Assert.Null(claims.Scope);
        Assert.Equal(new[] { org.ToString() }, claims.Organizations);
        Assert.Equal("Inmediato Ex Venerable Maestro · Taller 00", SupportViewProfiles.DisplayName(p));
        Assert.False(SupportViewProfiles.AllowsRoute(p, "/api/export.csv"));
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
