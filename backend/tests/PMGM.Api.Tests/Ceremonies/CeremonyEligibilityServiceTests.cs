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

    [Fact]
    public void WageIncrease_IsBlocked_WhenAdvancementRequirementsDoNotComply()
    {
        var result = _service.Evaluate(
            new CeremonyEligibilityInput(
                CeremonyCodes.Type.WageIncrease,
                FullyApprovedValidations(),
                null,
                null,
                20,
                AdvancementThresholds: new AdvancementThresholds(8, 4, 2, "rule-v1"),
                AdvancementEvidence: new AdvancementEvidence(7, 3, 1)),
            Now);

        Assert.False(result.IsEligible);
        Assert.NotNull(result.Advancement);
        Assert.Equal(1, result.Advancement!.SourceDegree);
        Assert.Equal(AdvancementEligibilityModes.Blocked, result.Advancement.Mode);
        Assert.Contains(result.BlockingReasons, x => x.Contains("advancement.meeting_attendance"));
    }

    [Fact]
    public void Exaltation_CanProceedByDispensationOnlyAfterInternalAffairsApproval()
    {
        var result = _service.Evaluate(
            new CeremonyEligibilityInput(
                CeremonyCodes.Type.Exaltation,
                FullyApprovedValidations(),
                null,
                null,
                20,
                AdvancementThresholds: new AdvancementThresholds(10, 5, 2, "rule-v2"),
                AdvancementEvidence: new AdvancementEvidence(8, 4, 1),
                Dispensation: new DispensationEvidence(
                    CouncilApproved: true,
                    CouncilRecordReference: "ACTA-DEMO-010",
                    InternalAffairsStatus: CeremonyCodes.ValidationStatus.ExceptionApproved,
                    InternalAffairsResolutionReference: "RI-DEMO-010")),
            Now);

        Assert.True(result.IsEligible);
        Assert.NotNull(result.Advancement);
        Assert.Equal(2, result.Advancement!.SourceDegree);
        Assert.Equal(AdvancementEligibilityModes.Dispensation, result.Advancement.Mode);
        Assert.Equal(AdvancementDispensationStatuses.Approved, result.Advancement.Dispensation?.Status);
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
