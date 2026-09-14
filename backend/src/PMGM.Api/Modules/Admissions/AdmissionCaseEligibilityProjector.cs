using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public sealed record AdmissionCaseEligibilityProjection(
    AdmissionEligibilityDecision Decision,
    Guid? WithdrawalLetterId,
    Guid? ThirdDegreeDecisionId,
    Guid? FirstDegreeBallotDecisionId,
    Guid? GrandMasterSpecialAcceptanceDecisionId);

public static class AdmissionCaseEligibilityProjector
{
    public static AdmissionCaseEligibilityProjection Evaluate(AdmissionCase admissionCase)
    {
        var withdrawalLetter = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.WithdrawalLetter, approvedOnly: false);
        var initiationEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedInitiation, approvedOnly: true);
        var wageEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedWageIncrease, approvedOnly: true);
        var exaltationEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedExaltation, approvedOnly: true);
        var degreeEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.Degree, approvedOnly: true);

        var signature = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.WithdrawalLetterHandwrittenSignature);
        var thirdDegree = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval);
        var firstDegree = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreeBallot);
        var grandMasterSpecial = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.GrandMasterSpecialAcceptance);

        var decision = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: admissionCase.AdmissionType,
            AffiliationMode: admissionCase.AffiliationMode,
            WithdrawalLetterAttached: withdrawalLetter is not null,
            WithdrawalLetterHandwrittenSignatureVerified: signature?.Status == CeremonyCodes.ValidationStatus.Approved,
            LodgeThirdDegreeApproved: ToDecisionState(thirdDegree),
            LodgeFirstDegreeBallotApproved: ToDecisionState(firstDegree),
            LegalizedInitiationEvidenceAttached: initiationEvidence is not null,
            WageIncreaseEvidenceApplies: admissionCase.WageIncreaseEvidenceApplies,
            LegalizedWageIncreaseEvidenceAttached: wageEvidence is not null,
            ExaltationEvidenceApplies: admissionCase.ExaltationEvidenceApplies,
            LegalizedExaltationEvidenceAttached: exaltationEvidence is not null,
            DegreeEvidenceAttached: degreeEvidence is not null,
            HasPeaceAndFriendshipPact: admissionCase.HasPeaceAndFriendshipPact,
            GrandMasterSpecialAcceptanceApproved: grandMasterSpecial?.Status == CeremonyCodes.ValidationStatus.Approved,
            PreviousRejectionDate: admissionCase.PreviousRejectionDate,
            NewPresentationDate: ChileDate(admissionCase.CreatedAtUtc),
            RejectionCausesRemedied: admissionCase.RejectionCausesRemedied));

        return new AdmissionCaseEligibilityProjection(
            decision,
            withdrawalLetter?.Id,
            thirdDegree?.Id,
            firstDegree?.Id,
            grandMasterSpecial?.Id);
    }

    private static AdmissionEvidence? LatestEvidence(AdmissionCase admissionCase, string evidenceType, bool approvedOnly)
        => admissionCase.Evidence
            .Where(x => x.EvidenceType == evidenceType &&
                        (!approvedOnly
                            ? x.ReviewStatus != CeremonyCodes.ValidationStatus.Rejected
                            : x.ReviewStatus == CeremonyCodes.ValidationStatus.Approved))
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.CreatedAtUtc)
            .FirstOrDefault();

    private static AdmissionDecision? LatestDecision(AdmissionCase admissionCase, string decisionType)
        => admissionCase.Decisions
            .Where(x => x.DecisionType == decisionType)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefault();

    private static bool? ToDecisionState(AdmissionDecision? decision)
        => decision?.Status switch
        {
            CeremonyCodes.ValidationStatus.Approved => true,
            CeremonyCodes.ValidationStatus.Rejected => false,
            _ => null
        };

    private static DateOnly ChileDate(DateTimeOffset instant)
    {
        var chile = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(instant, "America/Santiago");
        return DateOnly.FromDateTime(chile.DateTime);
    }
}
