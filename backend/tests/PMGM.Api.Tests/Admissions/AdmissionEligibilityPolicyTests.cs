using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Admissions;

public sealed class AdmissionEligibilityPolicyTests
{
    [Fact]
    public void Affiliation_Complies_WhenProtocolRequirementsAreApproved()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true));

        Assert.True(result.CanProceed);
        Assert.Equal("complies", result.Status);
        Assert.All(result.Requirements, x => Assert.Equal(CeremonyCodes.ValidationStatus.Approved, x.Status));
    }

    [Fact]
    public void Affiliation_IsObserved_WhenLodgeDecisionsArePending()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true));

        Assert.False(result.CanProceed);
        Assert.Equal("observed", result.Status);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.LodgeThirdDegreeApproval &&
            x.Status == CeremonyCodes.ValidationStatus.Observed);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.LodgeFirstDegreeBallot &&
            x.Status == CeremonyCodes.ValidationStatus.Observed);
    }

    [Fact]
    public void Affiliation_IsBlocked_WhenHandwrittenSignatureWasNotVerified()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Activation,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: false,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true));

        Assert.False(result.CanProceed);
        Assert.Equal("does_not_comply", result.Status);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.WithdrawalLetterHandwrittenSignature &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
    }

    [Fact]
    public void Affiliation_RePresentationRequiresOneYearAndRemediedCauses()
    {
        var rejectionDate = new DateOnly(2025, 9, 10);

        AdmissionEligibilityInput Input(DateOnly newDate, bool remedied) => new(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            PreviousRejectionDate: rejectionDate,
            NewPresentationDate: newDate,
            RejectionCausesRemedied: remedied);

        var tooEarly = AdmissionEligibilityPolicy.Evaluate(Input(new DateOnly(2026, 9, 9), true));
        var notRemedied = AdmissionEligibilityPolicy.Evaluate(Input(new DateOnly(2026, 9, 10), false));
        var allowed = AdmissionEligibilityPolicy.Evaluate(Input(new DateOnly(2026, 9, 10), true));

        Assert.False(tooEarly.CanProceed);
        Assert.False(notRemedied.CanProceed);
        Assert.True(allowed.CanProceed);
    }

    [Fact]
    public void Incorporation_IsBlocked_WhenLegalizedOriginEvidenceIsMissing()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: false,
            DegreeEvidenceAttached: false,
            HasPeaceAndFriendshipPact: true));

        Assert.False(result.CanProceed);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.LegalizedInitiationEvidence &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.DegreeEvidence &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
    }

    [Fact]
    public void Incorporation_IsObserved_WhenPactStatusIsUnknown()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            HasPeaceAndFriendshipPact: null));

        Assert.False(result.CanProceed);
        Assert.Equal("observed", result.Status);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.PeaceAndFriendshipPact &&
            x.Status == CeremonyCodes.ValidationStatus.Observed);
    }

    [Fact]
    public void Incorporation_WithoutPactRequiresGrandMasterSpecialAcceptance()
    {
        AdmissionEligibilityInput Input(bool gmApproved) => new(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            HasPeaceAndFriendshipPact: false,
            GrandMasterSpecialAcceptanceApproved: gmApproved);

        var blocked = AdmissionEligibilityPolicy.Evaluate(Input(false));
        var allowed = AdmissionEligibilityPolicy.Evaluate(Input(true));

        Assert.False(blocked.CanProceed);
        Assert.Contains(blocked.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.GrandMasterSpecialAcceptance &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
        Assert.True(allowed.CanProceed);
    }

    [Fact]
    public void Incorporation_RequiresWageAndExaltationEvidenceOnlyWhenApplicable()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            WageIncreaseEvidenceApplies: true,
            LegalizedWageIncreaseEvidenceAttached: false,
            ExaltationEvidenceApplies: true,
            LegalizedExaltationEvidenceAttached: false,
            DegreeEvidenceAttached: true,
            HasPeaceAndFriendshipPact: true));

        Assert.False(result.CanProceed);
        Assert.Contains(result.Requirements, x => x.Code == AdmissionCodes.Requirement.LegalizedWageIncreaseEvidence);
        Assert.Contains(result.Requirements, x => x.Code == AdmissionCodes.Requirement.LegalizedExaltationEvidence);
    }
}
