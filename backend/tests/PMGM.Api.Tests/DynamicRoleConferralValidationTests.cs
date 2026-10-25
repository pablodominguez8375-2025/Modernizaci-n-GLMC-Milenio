using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class DynamicRoleConferralValidationTests
{
    [Theory]
    [InlineData("own-lodge", InstitutionalRoles.TallerVenerable)]
    [InlineData("lodge", InstitutionalRoles.TallerSecretaria)]
    [InlineData("order", InstitutionalRoles.GranSecretaria)]
    [InlineData("order", InstitutionalRoles.RegimenInterior)]
    [InlineData("order", InstitutionalRoles.GranTesoreria)]
    [InlineData("order", InstitutionalRoles.GranHospitalaria)]
    [InlineData("order", InstitutionalRoles.PrivacyOfficer)]
    [InlineData("own-lodge", InstitutionalRoles.TallerTesoreria)]
    [InlineData("lodge", InstitutionalRoles.TallerHospitalaria)]
    public void Etapa2_permite_cargos(string scope, string role)
        => Assert.Null(DynamicRoleConferral.Validate([role], scope));

    [Theory]
    [InlineData("order", InstitutionalRoles.GranMaestria)]
    [InlineData("order", InstitutionalRoles.GranLogiaAdmin)]
    [InlineData("order", InstitutionalRoles.PlatformSuperAdmin)]
    [InlineData("own-lodge", InstitutionalRoles.TallerAdmin)]
    [InlineData("own-lodge", InstitutionalRoles.GranSecretaria)]
    [InlineData("order", InstitutionalRoles.TallerVenerable)]
    public void Sigue_rechazando_administracion_gran_maestria_o_alcance_incorrecto(string scope, string role)
        => Assert.NotNull(DynamicRoleConferral.Validate([role], scope));

    [Fact]
    public void Areas_sensibles_exigen_motivo_valido()
    {
        Assert.True(DynamicRoleConferral.HasSensitive([InstitutionalRoles.GranTesoreria]));
        Assert.False(DynamicRoleConferral.HasSensitive([InstitutionalRoles.GranSecretaria]));
        Assert.Equal([InstitutionalRoles.PrivacyOfficer], DynamicRoleConferral.NewSensitive([InstitutionalRoles.GranTesoreria], [InstitutionalRoles.GranTesoreria, InstitutionalRoles.PrivacyOfficer]));
        Assert.False(DynamicRoleConferral.ValidReason(null));
        Assert.False(DynamicRoleConferral.ValidReason("corto"));
        Assert.True(DynamicRoleConferral.ValidReason("Reemplazo temporal del Gran Tesorero"));
        Assert.False(DynamicRoleConferral.ValidReason(new string('x', 501)));
    }
}
