using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Admissions;

public sealed class AdmissionEligibilityPolicyTests
{
    [Fact]
    public void Affiliation_Complies_WhenModeAndWithdrawalLetterAreVerified()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true));

        Assert.True(result.CanProceed);
        Assert.Equal("complies", result.Status);
        Assert.All(result.Requirements, x => Assert.Equal(CeremonyCodes.ValidationStatus.Approved, x.Status));
    }

    [Fact]
    public void Affiliation_IsBlocked_WhenHandwrittenSignatureWasNotVerified()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Activation,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: false));

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

        var tooEarly = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            PreviousRejectionDate: rejectionDate,
            NewPresentationDate: new DateOnly(2026, 9, 9),
            RejectionCausesRemedied: true));

        var notRemedied = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            PreviousRejectionDate: rejectionDate,
            NewPresentationDate: new DateOnly(2026, 9, 10),
            RejectionCausesRemedied: false));

        var allowed = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            PreviousRejectionDate: rejectionDate,
            NewPresentationDate: new DateOnly(2026, 9, 10),
            RejectionCausesRemedied: true));

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
        var blocked = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            HasPeaceAndFriendshipPact: false,
            GrandMasterSpecialAcceptanceApproved: false));

        var allowed = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            HasPeaceAndFriendshipPact: false,
            GrandMasterSpecialAcceptanceApproved: true));

        Assert.False(blocked.CanProceed);
        Assert.Contains(blocked.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.GrandMasterSpecialAcceptance &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
        Assert.True(allowed.CanProceed);
    }

    [Fact]
    public void Incorporation_RequiresWageAndExaltationEvidenceOnlyWhenApplicable()
    {
        var missingApplicableEvidence = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            LegalizedInitiationEvidenceAttached: true,
            WageIncreaseEvidenceApplies: true,
            LegalizedWageIncreaseEvidenceAttached: false,
            ExaltationEvidenceApplies: true,
            LegalizedExaltationEvidenceAttached: false,
            DegreeEvidenceAttached: true,
            HasPeaceAndFriendshipPact: true));

        Assert.False(missingApplicableEvidence.CanProceed);
        Assert.Contains(missingApplicableEvidence.Requirements, x => x.Code == AdmissionCodes.Requirement.LegalizedWageIncreaseEvidence);
        Assert.Contains(missingApplicableEvidence.Requirements, x => x.Code == AdmissionCodes.Requirement.LegalizedExaltationEvidence);
    }
}
