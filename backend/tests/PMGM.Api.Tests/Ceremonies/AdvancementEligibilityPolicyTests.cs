using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementEligibilityPolicyTests
{
    private static readonly AdvancementThresholds Thresholds = new(
        MinimumMeetingAttendance: 8,
        MinimumInstructionAttendance: 4,
        MinimumWorkPapers: 2,
        RuleVersion: "demo-rule-v1");

    [Fact]
    public void WageIncrease_EvaluatesFirstDegreeRequirements()
    {
        var result = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.WageIncrease,
            Thresholds,
            new AdvancementEvidence(8, 4, 2));

        Assert.True(result.Applies);
        Assert.True(result.CanProceed);
        Assert.Equal(1, result.SourceDegree);
        Assert.Equal(AdvancementEligibilityModes.Ordinary, result.Mode);
        Assert.All(result.Requirements, requirement => Assert.True(requirement.Complies));
    }

    [Fact]
    public void Exaltation_EvaluatesSecondDegreeRequirements()
    {
        var result = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Exaltation,
            Thresholds,
            new AdvancementEvidence(9, 5, 3));

        Assert.True(result.CanProceed);
        Assert.Equal(2, result.SourceDegree);
        Assert.Equal("demo-rule-v1", result.RuleVersion);
    }

    [Fact]
    public void MissingOrdinaryRequirement_BlocksWithoutDispensation()
    {
        var result = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Exaltation,
            Thresholds,
            new AdvancementEvidence(9, 2, 1));

        Assert.False(result.CanProceed);
        Assert.Equal(AdvancementEligibilityModes.Blocked, result.Mode);
        Assert.Contains(result.Requirements, x => x.Code == AdvancementRequirementCodes.InstructionAttendance && !x.Complies);
        Assert.Contains(result.Requirements, x => x.Code == AdvancementRequirementCodes.WorkPapers && !x.Complies);
        Assert.Null(result.Dispensation);
    }

    [Fact]
    public void CouncilApprovalAlone_DoesNotEnableDispensation()
    {
        var result = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.WageIncrease,
            Thresholds,
            new AdvancementEvidence(6, 4, 2),
            new DispensationEvidence(
                CouncilApproved: true,
                CouncilRecordReference: "ACTA-DEMO-001",
                InternalAffairsStatus: CeremonyCodes.ValidationStatus.Pending,
                InternalAffairsResolutionReference: null));

        Assert.False(result.CanProceed);
        Assert.Equal(AdvancementEligibilityModes.Blocked, result.Mode);
        Assert.Equal(AdvancementDispensationStatuses.PendingInternalAffairs, result.Dispensation?.Status);
    }

    [Fact]
    public void Dispensation_RequiresCouncilAndInternalAffairsApproval()
    {
        var result = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Exaltation,
            Thresholds,
            new AdvancementEvidence(7, 3, 1),
            new DispensationEvidence(
                CouncilApproved: true,
                CouncilRecordReference: "ACTA-DEMO-002",
                InternalAffairsStatus: CeremonyCodes.ValidationStatus.ExceptionApproved,
                InternalAffairsResolutionReference: "RI-DEMO-002"));

        Assert.True(result.CanProceed);
        Assert.Equal(AdvancementEligibilityModes.Dispensation, result.Mode);
        Assert.Equal(AdvancementDispensationStatuses.Approved, result.Dispensation?.Status);
    }

    [Fact]
    public void RejectedCouncilVote_CannotBeOverriddenByInternalAffairsStatus()
    {
        var result = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.WageIncrease,
            Thresholds,
            new AdvancementEvidence(3, 1, 0),
            new DispensationEvidence(
                CouncilApproved: false,
                CouncilRecordReference: "ACTA-DEMO-003",
                InternalAffairsStatus: CeremonyCodes.ValidationStatus.ExceptionApproved,
                InternalAffairsResolutionReference: "RI-DEMO-003"));

        Assert.False(result.CanProceed);
        Assert.Equal(AdvancementDispensationStatuses.Rejected, result.Dispensation?.Status);
    }

    [Fact]
    public void Initiation_DoesNotApplyAdvancementRequirements()
    {
        var result = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Initiation,
            Thresholds,
            new AdvancementEvidence(0, 0, 0));

        Assert.False(result.Applies);
        Assert.True(result.CanProceed);
        Assert.Equal(AdvancementEligibilityModes.NotApplicable, result.Mode);
        Assert.Empty(result.Requirements);
    }
}
