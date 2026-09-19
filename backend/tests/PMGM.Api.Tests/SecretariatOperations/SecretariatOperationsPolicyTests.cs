using PMGM.Api.Modules.SecretariatOperations;
using Xunit;

namespace PMGM.Api.Tests.SecretariatOperations;

public sealed class SecretariatOperationsPolicyTests
{
    [Theory]
    [InlineData("12.345.678-5")]
    [InlineData("12345678-5")]
    public void RutValidation_AcceptsCanonicalChileRut(string rut)
        => Assert.True(SecretariatOperationsPolicy.IsValidRut(rut));

    [Theory]
    [InlineData("12.345.678-9")]
    [InlineData("no-es-rut")]
    [InlineData("")]
    public void RutValidation_RejectsInvalidValues(string rut)
        => Assert.False(SecretariatOperationsPolicy.IsValidRut(rut));

    [Fact]
    public void HistoricalMilestones_AcceptUnknownDatesWithoutInventingValues()
        => Assert.True(SecretariatOperationsPolicy.HasChronologicalMilestones(
            initiation: new DateOnly(1998, 5, 12),
            wageIncrease: null,
            exaltation: new DateOnly(2002, 8, 24)));

    [Fact]
    public void HistoricalMilestones_RejectImpossibleSequence()
        => Assert.False(SecretariatOperationsPolicy.HasChronologicalMilestones(
            initiation: new DateOnly(2000, 1, 1),
            wageIncrease: new DateOnly(1999, 1, 1),
            exaltation: new DateOnly(2002, 1, 1)));

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(SecretariatOperationsCodes.CeremonyType.Initiation, false)]
    [InlineData(SecretariatOperationsCodes.CeremonyType.Affiliation, false)]
    [InlineData(SecretariatOperationsCodes.CeremonyType.WageIncrease, false)]
    [InlineData(SecretariatOperationsCodes.CeremonyType.Exaltation, false)]
    [InlineData(SecretariatOperationsCodes.CeremonyType.Incorporation, false)]
    public void WorkPaper_IsOptionalButOnlyAllowedForNonCeremonialTenidas(string? ceremonyType, bool expected)
        => Assert.Equal(
            expected,
            SecretariatOperationsPolicy.WorkPaperAllowed(
                SecretariatOperationsCodes.RecordType.LodgeMeeting,
                ceremonyType));

    [Theory]
    [InlineData(SecretariatOperationsCodes.RecordType.AdministrativeMeeting)]
    [InlineData(SecretariatOperationsCodes.RecordType.Council)]
    public void WorkPaper_IsNeverAttachedToMeetingOrCouncil(string recordType)
        => Assert.False(SecretariatOperationsPolicy.WorkPaperAllowed(recordType, null));
}
