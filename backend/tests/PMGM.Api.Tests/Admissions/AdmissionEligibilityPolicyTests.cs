using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.CandidateIntake;
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
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Standard,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
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
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Standard,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true));

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
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Standard,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: false,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
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
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Standard,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
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
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: false,
            DegreeEvidenceAttached: false,
            OriginObedienceRecognizedAsRegular: true,
            InformationCommissionRequired: true,
            InformationCommissionAppointed: true,
            InformationCommissionCompleted: true,
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
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            OriginObedienceRecognizedAsRegular: true,
            InformationCommissionRequired: true,
            InformationCommissionAppointed: true,
            InformationCommissionCompleted: true,
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
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            OriginObedienceRecognizedAsRegular: true,
            InformationCommissionRequired: true,
            InformationCommissionAppointed: true,
            InformationCommissionCompleted: true,
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
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            WageIncreaseEvidenceApplies: true,
            LegalizedWageIncreaseEvidenceAttached: false,
            ExaltationEvidenceApplies: true,
            LegalizedExaltationEvidenceAttached: false,
            DegreeEvidenceAttached: true,
            OriginObedienceRecognizedAsRegular: true,
            InformationCommissionRequired: true,
            InformationCommissionAppointed: true,
            InformationCommissionCompleted: true,
            HasPeaceAndFriendshipPact: true));

        Assert.False(result.CanProceed);
        Assert.Contains(result.Requirements, x => x.Code == AdmissionCodes.Requirement.LegalizedWageIncreaseEvidence);
        Assert.Contains(result.Requirements, x => x.Code == AdmissionCodes.Requirement.LegalizedExaltationEvidence);
    }
    [Theory]
    [InlineData(3, 2, 1, 0, true)]
    [InlineData(6, 4, 2, 0, true)]
    [InlineData(6, 3, 2, 1, false)]
    [InlineData(5, 3, 2, 0, false)]
    [InlineData(5, 4, 1, 0, true)]
    public void ThirdDegree_EnforcesTwoThirdsOfPresentMasters(
        int present, int favor, int against, int abstentions, bool expected)
    {
        var result = AdmissionEligibilityPolicy.EvaluateThirdDegreeVote(
            present, favor, against, abstentions);
        Assert.Equal(expected, result.CanProceed);
        Assert.Equal(!expected, result.IsRejected);
    }

    [Fact]
    public void ThirdDegree_RejectsInvalidAggregatesWithoutRecordingOutcome()
    {
        var result = AdmissionEligibilityPolicy.EvaluateThirdDegreeVote(6, 4, 1, 0);
        Assert.False(result.CanProceed);
        Assert.False(result.IsRejected);
        Assert.Equal("admission.third_degree.counts", result.Code);
    }

    [Fact]
    public void Article23_ImpedimentRequiresGrandMasterPardon()
    {
        AdmissionEligibilityInput Input(bool pardon) => new(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Standard,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: false,
            GrandMasterPardonApproved: pardon,
            FirstDegreePresentationRecorded: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true);
        var blocked = AdmissionEligibilityPolicy.Evaluate(Input(false));
        Assert.False(blocked.CanProceed);
        Assert.Contains(blocked.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.Article23Clearance &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
        Assert.True(AdmissionEligibilityPolicy.Evaluate(Input(true)).CanProceed);
    }

    [Fact]
    public void FirstDegreePresentation_IsRequiredForAffiliation()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Standard,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true));
        Assert.False(result.CanProceed);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.FirstDegreePresentation &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
    }

    [Fact]
    public void Incorporation_RequiresThreeMasterCommissionAndItsCompletion()
    {
        AdmissionEligibilityInput Input(bool appointed, bool completed) => new(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            InformationCommissionRequired: true,
            InformationCommissionAppointed: appointed,
            InformationCommissionCompleted: completed,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            OriginObedienceRecognizedAsRegular: true,
            HasPeaceAndFriendshipPact: true);
        Assert.False(AdmissionEligibilityPolicy.Evaluate(Input(false, false)).CanProceed);
        Assert.False(AdmissionEligibilityPolicy.Evaluate(Input(true, false)).CanProceed);
        Assert.True(AdmissionEligibilityPolicy.Evaluate(Input(true, true)).CanProceed);
    }

    [Fact]
    public void Incorporation_RegularityRecognitionAndPactAreIndependent()
    {
        AdmissionEligibilityInput Input(bool recognition, bool pactDecision) => new(
            AdmissionType: CeremonyCodes.Type.Incorporation,
            AffiliationMode: null,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            InformationCommissionRequired: true,
            InformationCommissionAppointed: true,
            InformationCommissionCompleted: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true,
            LegalizedInitiationEvidenceAttached: true,
            DegreeEvidenceAttached: true,
            OriginObedienceRecognizedAsRegular: false,
            GrandMasterRegularityRecognitionApproved: recognition,
            HasPeaceAndFriendshipPact: false,
            GrandMasterSpecialAcceptanceApproved: pactDecision);
        Assert.False(AdmissionEligibilityPolicy.Evaluate(Input(true, false)).CanProceed);
        Assert.False(AdmissionEligibilityPolicy.Evaluate(Input(false, true)).CanProceed);
        Assert.True(AdmissionEligibilityPolicy.Evaluate(Input(true, true)).CanProceed);
    }

    [Fact]
    public void BallotReusesInitiationRoundValidation()
    {
        var invalid = AdmissionEligibilityPolicy.EvaluateFirstDegreeBallot(
            new[] { new CandidateBallotRound(1, 5, 3, 1) }, approved: true);
        Assert.False(invalid.CanProceed);
        Assert.False(invalid.IsRejected);
        var valid = AdmissionEligibilityPolicy.EvaluateFirstDegreeBallot(
            new[] { new CandidateBallotRound(1, 5, 4, 1) }, approved: true);
        Assert.True(valid.CanProceed);
    }


    [Fact]
    public void Reentry_RequiresInformationCommission()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Reentry,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            InformationCommissionRequired: AdmissionProcedureRules.RequiresInformationCommission(
                CeremonyCodes.Type.Affiliation,
                AdmissionCodes.AffiliationProcedure.Reentry),
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true));

        Assert.False(result.CanProceed);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.InformationCommission &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
    }

    [Fact]
    public void Transfer_CanUseCamaraDelMedioCommissionWaiver()
    {
        var result = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: CeremonyCodes.Type.Affiliation,
            AffiliationMode: AdmissionCodes.AffiliationMode.Simple,
            AffiliationProcedure: AdmissionCodes.AffiliationProcedure.Transfer,
            WithdrawalLetterAttached: true,
            WithdrawalLetterHandwrittenSignatureVerified: true,
            Article23Clear: true,
            FirstDegreePresentationRecorded: true,
            InformationCommissionRequired: AdmissionProcedureRules.RequiresInformationCommission(
                CeremonyCodes.Type.Affiliation,
                AdmissionCodes.AffiliationProcedure.Transfer),
            InformationCommissionWaived: true,
            LodgeThirdDegreeApproved: true,
            LodgeFirstDegreeBallotApproved: true));

        Assert.True(result.CanProceed);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.InformationCommission &&
            x.Status == CeremonyCodes.ValidationStatus.Approved);
        Assert.Contains(result.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.InformationCommissionCompleted &&
            x.Status == CeremonyCodes.ValidationStatus.Approved);
    }

}
