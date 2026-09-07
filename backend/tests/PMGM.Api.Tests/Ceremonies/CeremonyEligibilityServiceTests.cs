using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class CeremonyEligibilityServiceTests
{
    private readonly CeremonyEligibilityService _service = new();
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ceremony_IsBlocked_WhenTreasuryIsNotApproved()
    {
        var result = _service.Evaluate(
            new CeremonyEligibilityInput(
                CeremonyCodes.Type.Exaltation,
                new Dictionary<string, string>
                {
                    [CeremonyCodes.ValidationType.InternalAffairs] = CeremonyCodes.ValidationStatus.Approved,
                    [CeremonyCodes.ValidationType.Treasury] = CeremonyCodes.ValidationStatus.Rejected,
                    [CeremonyCodes.ValidationType.Hospitalaria] = CeremonyCodes.ValidationStatus.Approved
                },
                null,
                null,
                20),
            Now);

        Assert.False(result.IsEligible);
        Assert.Contains(result.BlockingReasons, x => x.Contains(CeremonyCodes.ValidationType.Treasury));
    }

    [Fact]
    public void Ceremony_IsBlocked_WhenHospitalariaIsNotApproved()
    {
        var result = _service.Evaluate(
            new CeremonyEligibilityInput(
                CeremonyCodes.Type.WageIncrease,
                new Dictionary<string, string>
                {
                    [CeremonyCodes.ValidationType.InternalAffairs] = CeremonyCodes.ValidationStatus.Approved,
                    [CeremonyCodes.ValidationType.Treasury] = CeremonyCodes.ValidationStatus.Approved,
                    [CeremonyCodes.ValidationType.Hospitalaria] = CeremonyCodes.ValidationStatus.Observed
                },
                null,
                null,
                20),
            Now);

        Assert.False(result.IsEligible);
        Assert.Contains(result.BlockingReasons, x => x.Contains(CeremonyCodes.ValidationType.Hospitalaria));
    }

    [Fact]
    public void Initiation_IsBlocked_BeforeConfiguredPublicationDays()
    {
        var result = _service.Evaluate(
            new CeremonyEligibilityInput(
                CeremonyCodes.Type.Initiation,
                FullyApprovedValidations(),
                Now.AddDays(-19),
                null,
                20),
            Now);

        Assert.False(result.IsEligible);
        Assert.Equal(19, result.PublicationElapsedDays);
        Assert.Equal(20, result.PublicationRequiredDays);
    }

    [Fact]
    public void Initiation_IsEligible_WhenAllValidationsAndPublicationComply()
    {
        var result = _service.Evaluate(
            new CeremonyEligibilityInput(
                CeremonyCodes.Type.Initiation,
                FullyApprovedValidations(),
                Now.AddDays(-20),
                null,
                20),
            Now);

        Assert.True(result.IsEligible);
        Assert.Empty(result.BlockingReasons);
        Assert.Equal(20, result.PublicationElapsedDays);
    }

    [Fact]
    public void FormalException_IsAnEnablingValidationStatus()
    {
        var validations = FullyApprovedValidations();
        validations[CeremonyCodes.ValidationType.Hospitalaria] = CeremonyCodes.ValidationStatus.ExceptionApproved;

        var result = _service.Evaluate(
            new CeremonyEligibilityInput(
                CeremonyCodes.Type.Exaltation,
                validations,
                null,
                null,
                20),
            Now);

        Assert.True(result.IsEligible);
    }

    private static Dictionary<string, string> FullyApprovedValidations()
        => new()
        {
            [CeremonyCodes.ValidationType.InternalAffairs] = CeremonyCodes.ValidationStatus.Approved,
            [CeremonyCodes.ValidationType.Treasury] = CeremonyCodes.ValidationStatus.Approved,
            [CeremonyCodes.ValidationType.Hospitalaria] = CeremonyCodes.ValidationStatus.Approved,
            [CeremonyCodes.ValidationType.CandidatePublication] = CeremonyCodes.ValidationStatus.Approved
        };
}
