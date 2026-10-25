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
    public void Etapa2_permite_cargos_no_financieros(string scope, string role)
        => Assert.Null(DynamicRoleConferral.Validate([role], scope));

    [Theory]
    [InlineData("order", InstitutionalRoles.GranTesoreria)]
    [InlineData("order", InstitutionalRoles.GranHospitalaria)]
    [InlineData("order", InstitutionalRoles.PrivacyOfficer)]
    [InlineData("order", InstitutionalRoles.GranMaestria)]
    [InlineData("order", InstitutionalRoles.GranLogiaAdmin)]
    [InlineData("order", InstitutionalRoles.PlatformSuperAdmin)]
    [InlineData("own-lodge", InstitutionalRoles.TallerTesoreria)]
    [InlineData("own-lodge", InstitutionalRoles.TallerHospitalaria)]
    [InlineData("own-lodge", InstitutionalRoles.TallerAdmin)]
    [InlineData("own-lodge", InstitutionalRoles.GranSecretaria)]
    [InlineData("order", InstitutionalRoles.TallerVenerable)]
    public void Sigue_rechazando_areas_sensibles_o_alcance_incorrecto(string scope, string role)
        => Assert.NotNull(DynamicRoleConferral.Validate([role], scope));
}
