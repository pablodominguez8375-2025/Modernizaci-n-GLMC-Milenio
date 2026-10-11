using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>Solo la última resolución formal y favorable de Régimen Interior habilita una reinsinuación.</summary>
public static class CandidateRePresentationReviewPolicy
{
    public static bool CanResolve(string? status, string? sourceReference, string? notes)
        => status is CeremonyCodes.ValidationStatus.Approved or CeremonyCodes.ValidationStatus.Observed
            or CeremonyCodes.ValidationStatus.Rejected
            && !string.IsNullOrWhiteSpace(sourceReference)
            && sourceReference.Trim().Length <= 240
            && (notes is null || notes.Length <= 1000);

    public static bool CanAuthorize(string? latestStatus)
        => latestStatus == CeremonyCodes.ValidationStatus.Approved;

    public static string EligibilityStatus(string? latestStatus)
        => latestStatus == CeremonyCodes.ValidationStatus.Approved
            ? CeremonyCodes.ValidationStatus.Approved
            : latestStatus == CeremonyCodes.ValidationStatus.Rejected
                ? CeremonyCodes.ValidationStatus.Rejected
                : CeremonyCodes.ValidationStatus.Observed;
}
