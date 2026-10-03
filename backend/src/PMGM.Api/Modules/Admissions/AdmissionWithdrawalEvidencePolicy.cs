using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionWithdrawalEvidencePolicy
{
    // Replacements never fall back to an earlier approved document.
    public static AdmissionEvidence? CurrentLetter(AdmissionCase admissionCase)
        => admissionCase.Evidence.Where(x => x.EvidenceType == AdmissionWorkflowCodes.EvidenceType.WithdrawalLetter)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).FirstOrDefault();

    public static bool HasValidDate(AdmissionCase admissionCase, AdmissionEvidence letter, DateOnly today)
        => letter.EvidenceDate is { } granted && granted <= today &&
           (admissionCase.AdmissionType != CeremonyCodes.Type.Affiliation ||
            (AdmissionCodes.AffiliationMode.IsValid(admissionCase.AffiliationMode) && admissionCase.WithdrawalLetterGrantedDate == granted &&
             WithdrawalLetterPolicy.ModeAtCreation(granted, ChileDate(admissionCase.CreatedAtUtc)) == admissionCase.AffiliationMode));

    public static AdmissionDecision? VerifiedSignature(AdmissionCase admissionCase, DateOnly today)
    {
        var letter = CurrentLetter(admissionCase);
        if (letter is null || letter.DocumentVersionId is null ||
            letter.ReviewStatus != CeremonyCodes.ValidationStatus.Approved || letter.ReviewedAtUtc is null ||
            !HasValidDate(admissionCase, letter, today)) return null;
        var signature = admissionCase.Decisions
            .Where(x => x.DecisionType == AdmissionWorkflowCodes.DecisionType.WithdrawalSignature(letter.Id))
            .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).FirstOrDefault();
        return signature is not null && signature.Status == CeremonyCodes.ValidationStatus.Approved &&
               signature.AsOfDate >= letter.EvidenceDate!.Value && signature.AsOfDate <= today &&
               signature.RecordedAtUtc >= letter.ReviewedAtUtc && !string.IsNullOrWhiteSpace(signature.SourceReference)
            ? signature : null;
    }

    public static DateOnly ChileDate(DateTimeOffset instant)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(instant, "America/Santiago").DateTime);
}
