using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>
/// #1035 T2c: MATRIZ de ámbitos mixtos (R = Taller de baja, A = Taller activo, G = Gran Logia activa, U = org no verificable, O = scope order, M = Máster).
/// Cada fila declara qué ámbitos operativos sobreviven y si queda sesión de «Taller de baja». Fija el comportamiento actual (cargos planos incluidos).
/// </summary>
public sealed class RetiredWorkshopScopeMatrixTests
{
    private static readonly Guid R1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid R2 = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid A1 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid G1 = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid U1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Dictionary<Guid, WorkshopScopeState> States = new()
    {
        [R1] = WorkshopScopeState.Retired, [R2] = WorkshopScopeState.Retired, [A1] = WorkshopScopeState.Active, [G1] = WorkshopScopeState.Active,
        [U1] = WorkshopScopeState.Unusable
    };

    // orgs, order, master, expectedOrgs, expectedRoleKept, expectedMarker, expectedRetiredRefs
    public static TheoryData<string, string, bool, bool, string, bool, bool, int> Cases => new()
    {
        { "R", "R", false, false, "", false, true, 1 },
        { "R+R", "R,R2", false, false, "", false, true, 2 },
        { "R+A", "R,A", false, false, "A", true, false, 1 },
        { "R+G", "R,G", false, false, "G", true, false, 1 },
        { "R+U", "R,U", false, false, "", false, true, 1 },
        { "R+A+U", "R,A,U", false, false, "A", true, false, 1 },
        { "A+U", "A,U", false, false, "A", true, false, 0 },
        { "U", "U", false, false, "", false, false, 0 },
        { "R+O", "R", true, false, "", true, false, 1 },
        { "R+A+O", "R,A", true, false, "A", true, false, 1 },
        { "U+O", "U", true, false, "", true, false, 0 },
        { "R+Master", "R", true, true, "", true, false, 1 },
        { "A", "A", false, false, "A", true, false, 0 },
    };

    private static Guid Id(string k) => k switch { "R" => R1, "R2" => R2, "A" => A1, "G" => G1, _ => U1 };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Mixed_scope_matrix(string name, string orgs, bool order, bool master, string expectedOrgs, bool roleKept, bool marker, int retiredRefs)
    {
        var claims = new List<Claim> { new("sub", "u1"), new(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin) };
        foreach (var o in orgs.Split(',', StringSplitOptions.RemoveEmptyEntries)) claims.Add(new Claim(InstitutionalClaims.Organization, Id(o).ToString()));
        if (order) claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        if (master) claims.Add(new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", ClaimTypes.Role));

        var result = RetiredWorkshopScope.Apply(user, States);

        var kept = string.Join(",", result.FindAll(InstitutionalClaims.Organization).Select(x => x.Value == A1.ToString() ? "A" : x.Value == G1.ToString() ? "G" : x.Value == R1.ToString() ? "R" : "?").OrderBy(x => x));
        Assert.True(expectedOrgs == kept, $"[{name}] orgs conservadas '{kept}' ≠ '{expectedOrgs}'");
        Assert.True(roleKept == result.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.TallerAdmin), $"[{name}] cargo plano conservado={roleKept}");
        Assert.True(marker == result.HasClaim(RetiredWorkshopScope.Marker, "true"), $"[{name}] marcador de sesión de Taller de baja={marker}");
        Assert.Equal(retiredRefs, result.FindAll(RetiredWorkshopScope.RetiredOrg).Count());
    }
}
