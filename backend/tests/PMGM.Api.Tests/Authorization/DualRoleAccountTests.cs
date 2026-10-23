using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>
/// Regla del PO (23-10-2026): los perfiles son independientes del cargo y se asignan a mano;
/// un Hermano con un perfil en su Taller (Venerable) y otro en la Orden (Gran Primer Vigilante)
/// los tiene ambos en una sola cuenta, cada uno con su propio alcance.
/// </summary>
public sealed class DualRoleAccountTests
{
    private readonly InstitutionalAccessService access = new();
    private static readonly Guid Taller = Guid.NewGuid();
    private static readonly Guid OtroTaller = Guid.NewGuid();

    private static ClaimsPrincipal Account(string scope, Guid? organization, params string[] roles)
    {
        var claims = new List<Claim> { new("sub", "hermano-con-dos-perfiles"), new(InstitutionalClaims.Scope, scope) };
        if (organization is { } org) claims.Add(new(InstitutionalClaims.Organization, org.ToString()));
        claims.AddRange(roles.Select(r => new Claim(InstitutionalClaims.Role, r)));
        return new(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void One_account_holds_the_lodge_profile_and_the_order_profile_each_with_its_own_scope()
    {
        var both = Account("order", Taller, InstitutionalRoles.TallerVenerable, InstitutionalRoles.GranPrimerVigilante);
        // Perfil del Taller: solo en su Taller.
        Assert.True(access.CanApproveLodgeExpenses(both, Taller));
        Assert.False(access.CanApproveLodgeExpenses(both, OtroTaller));
        // Perfil de la Orden: instrucción de grado 2 (Compañero), no de otros grados.
        Assert.True(access.CanReadOrderLodgeInstructions(both, 2));
        Assert.False(access.CanReadOrderLodgeInstructions(both, 1));
        Assert.False(access.CanReadOrderLodgeInstructions(both, 3));
        var capabilities = SessionProfileBuilder.Build(both, access).Capabilities;
        Assert.True(capabilities.CanApproveLodgeExpenses);
        Assert.True(capabilities.CanReadOrderFellowcraftInstructions);
        Assert.False(capabilities.CanReadOrderApprenticeInstructions);
        Assert.False(capabilities.CanConfigureSystem);
    }

    [Fact]
    public void Each_profile_alone_does_not_grant_the_other()
    {
        var lodgeOnly = Account("organization", Taller, InstitutionalRoles.TallerVenerable);
        Assert.True(access.CanApproveLodgeExpenses(lodgeOnly, Taller));
        Assert.False(access.CanReadOrderLodgeInstructions(lodgeOnly, 2));
        var orderOnly = Account("order", null, InstitutionalRoles.GranPrimerVigilante);
        Assert.True(access.CanReadOrderLodgeInstructions(orderOnly, 2));
        Assert.False(access.CanApproveLodgeExpenses(orderOnly, Taller));
    }

    [Fact]
    public void The_order_profile_does_not_work_without_order_scope()
    {
        var wrongScope = Account("organization", Taller, InstitutionalRoles.TallerVenerable, InstitutionalRoles.GranPrimerVigilante);
        Assert.False(access.CanReadOrderLodgeInstructions(wrongScope, 2));
        Assert.True(access.CanApproveLodgeExpenses(wrongScope, Taller));
    }
}
