using System.Text.Json;
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
        var withdrawalLetter = AdmissionWithdrawalEvidencePolicy.CurrentLetter(admissionCase);
        var initiationEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedInitiation, approvedOnly: true);
        var wageEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedWageIncrease, approvedOnly: true);
        var exaltationEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedExaltation, approvedOnly: true);
        var degreeEvidence = LatestEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.Degree, approvedOnly: true);

        var signature = AdmissionWithdrawalEvidencePolicy.VerifiedSignature(admissionCase, AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow));
        var thirdDegree = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval);
        var firstDegree = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreeBallot);
        var grandMasterSpecial = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.GrandMasterSpecialAcceptance);

        var decision = AdmissionEligibilityPolicy.Evaluate(new AdmissionEligibilityInput(
            AdmissionType: admissionCase.AdmissionType,
            AffiliationMode: admissionCase.AffiliationMode,
            WithdrawalLetterAttached: withdrawalLetter?.ReviewStatus == CeremonyCodes.ValidationStatus.Approved,
            WithdrawalLetterHandwrittenSignatureVerified: signature?.Status == CeremonyCodes.ValidationStatus.Approved,
            LodgeThirdDegreeApproved: ToDecisionState(thirdDegree),
            LodgeFirstDegreeBallotApproved: ToDecisionState(firstDegree),
            LegalizedInitiationEvidenceAttached: initiationEvidence is not null,
            WageIncreaseEvidenceApplies: admissionCase.WageIncreaseEvidenceApplies || admissionCase.Degree is "fellowcraft" or "master",
            LegalizedWageIncreaseEvidenceAttached: wageEvidence is not null,
            ExaltationEvidenceApplies: admissionCase.ExaltationEvidenceApplies || admissionCase.Degree == "master",
            LegalizedExaltationEvidenceAttached: exaltationEvidence is not null,
            DegreeEvidenceAttached: degreeEvidence is not null,
            HasPeaceAndFriendshipPact: admissionCase.HasPeaceAndFriendshipPact,
            GrandMasterSpecialAcceptanceApproved: grandMasterSpecial?.Status == CeremonyCodes.ValidationStatus.Approved,
            PreviousRejectionDate: admissionCase.PreviousRejectionDate,
            NewPresentationDate: ChileDate(admissionCase.CreatedAtUtc),
            RejectionCausesRemedied: admissionCase.RejectionCausesRemedied));

        var requirements = decision.Requirements.ToList();
        var today = AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow);
        bool Recorded(AdmissionDecision? value) => value is not null &&
            value.AsOfDate >= ChileDate(admissionCase.CreatedAtUtc) && value.AsOfDate <= today &&
            !string.IsNullOrWhiteSpace(value.SourceReference);
        void Require(string code, bool approved, string reason)
            => requirements.Add(new AdmissionRequirementResult(code,
                approved ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Observed, reason));

        var presentation = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreePresentation);
        Require("lodge_first_degree_presentation", Recorded(presentation) && presentation!.Status == CeremonyCodes.ValidationStatus.Approved &&
            thirdDegree is not null && presentation.AsOfDate <= thirdDegree.AsOfDate,
            "La solicitud escrita y sus antecedentes deben constar como leídos en primer grado antes de la decisión de tercer grado (art. 2.4).");
        var article23 = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.Article23Review);
        var pardon = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.GrandMasterPardon);
        Require("article_2_3_review", Recorded(article23) &&
            (article23!.Status == CeremonyCodes.ValidationStatus.Approved ||
             (article23.Status == CeremonyCodes.ValidationStatus.Rejected && Recorded(pardon) &&
              pardon!.Status == CeremonyCodes.ValidationStatus.Approved && pardon.AsOfDate >= article23.AsOfDate)),
            "Revisión de Régimen Interior del art. 2.3; si existe impedimento, indulto de Gran Maestría documentado posterior.");
        if (admissionCase.AdmissionType == CeremonyCodes.Type.Incorporation)
        {
            bool? recognized = null;
            try
            {
                using var review = JsonDocument.Parse(article23?.Notes ?? "{}");
                if (review.RootElement.TryGetProperty("OriginObedienceRecognized", out var value) &&
                    value.ValueKind is JsonValueKind.True or JsonValueKind.False) recognized = value.GetBoolean();
            }
            catch (JsonException) { /* No inferir reconocimiento desde una nota legado no estructurada. */ }
            var recognition = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.GrandMasterRegularityRecognition);
            Require("origin_regularity", recognized == true ||
                (recognized == false && Recorded(recognition) && recognition!.Status == CeremonyCodes.ValidationStatus.Approved &&
                 article23 is not null && recognition.AsOfDate >= article23.AsOfDate),
                "Régimen Interior debe acreditar si la Obediencia de origen es reconocida como regular; en caso negativo se exige reconocimiento o regularización expresa de Gran Maestría (art. 2.1).");
        }
        Require("lodge_ballot_chronology", Recorded(thirdDegree) && Recorded(firstDegree) &&
            firstDegree!.AsOfDate > thirdDegree!.AsOfDate,
            "El balotaje de primer grado debe celebrarse en una fecha posterior a la aprobación de tercer grado (art. 2.4).");
        if (admissionCase.AdmissionType == CeremonyCodes.Type.Incorporation ||
            admissionCase.AffiliationMode == AdmissionCodes.AffiliationMode.Activation)
        {
            var appointment = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.InformationCommissionAppointed);
            var conclusion = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.InformationCommissionCompleted);
            Require("information_commission", Recorded(appointment) && Recorded(conclusion) &&
                appointment!.Status == CeremonyCodes.ValidationStatus.Approved && conclusion!.Status == CeremonyCodes.ValidationStatus.Approved &&
                conclusion.RecordedAtUtc >= appointment.RecordedAtUtc && conclusion.AsOfDate >= appointment.AsOfDate &&
                thirdDegree is not null && conclusion.AsOfDate <= thirdDegree.AsOfDate,
                "Comisión de tres Maestros concluida tras el último nombramiento y antes de la decisión de tercer grado (art. 2.5).");
        }
        var rejected = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Rejected);
        var observed = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Observed);
        var status = rejected ? "does_not_comply" : observed ? "observed" : "complies";
        decision = new AdmissionEligibilityDecision(status == "complies", status, requirements);

        return new AdmissionCaseEligibilityProjection(
            decision,
            withdrawalLetter?.Id,
            thirdDegree?.Id,
            firstDegree?.Id,
            grandMasterSpecial?.Id);
    }

    private static AdmissionEvidence? LatestEvidence(AdmissionCase admissionCase, string evidenceType, bool approvedOnly)
    {
        var current = admissionCase.Evidence.Where(x => x.EvidenceType == evidenceType)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).FirstOrDefault();
        return current is not null && (approvedOnly ? current.ReviewStatus == CeremonyCodes.ValidationStatus.Approved :
            current.ReviewStatus != CeremonyCodes.ValidationStatus.Rejected) ? current : null;
    }

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
