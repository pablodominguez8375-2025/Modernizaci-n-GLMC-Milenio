using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.Ceremonies;

public static class CeremonyEligibilityPolicy
{
    // Sobrecarga de compatibilidad para consumidores antiguos. Los flujos institucionales
    // nuevos deben utilizar la firma que recibe explícitamente grandMasterStatus.
    public static CeremonyEligibilityDecision Evaluate(
        string ceremonyType,
        string? regimenInteriorStatus,
        string? treasuryStatus,
        string? hospitalariaStatus,
        CandidatePublicationEvidence? publication)
        => Evaluate(
            ceremonyType,
            regimenInteriorStatus,
            treasuryStatus,
            hospitalariaStatus,
            CeremonyCodes.ValidationStatus.Approved,
            publication);

    public static CeremonyEligibilityDecision Evaluate(
        string ceremonyType,
        string? regimenInteriorStatus,
        string? treasuryStatus,
        string? hospitalariaStatus,
        string? grandMasterStatus,
        CandidatePublicationEvidence? publication)
    {
        var requirements = new List<CeremonyRequirementResult>
        {
            EvaluateRegimenInterior(regimenInteriorStatus),
            EvaluateTreasury(treasuryStatus),
            EvaluateHospitalaria(hospitalariaStatus),
            EvaluateGrandMaster(grandMasterStatus)
        };

        if (ceremonyType == CeremonyCodes.Type.Initiation)
        {
            requirements.Add(EvaluatePublication(publication));
        }

        var overall = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Rejected)
            ? "does_not_comply"
            : requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Observed)
                ? "observed"
                : "complies";

        return new CeremonyEligibilityDecision(
            overall,
            overall == "complies",
            requirements);
    }

    private static CeremonyRequirementResult EvaluateRegimenInterior(string? status)
    {
        if (status is CeremonyCodes.ValidationStatus.Approved or CeremonyCodes.ValidationStatus.ExceptionApproved)
        {
            return new("regimen_interior", "Régimen Interior", CeremonyCodes.ValidationStatus.Approved, "Aprobación vigente registrada.");
        }

        if (status == CeremonyCodes.ValidationStatus.Observed)
        {
            return new("regimen_interior", "Régimen Interior", CeremonyCodes.ValidationStatus.Observed, "La solicitud tiene observaciones pendientes de Régimen Interior.");
        }

        return new("regimen_interior", "Régimen Interior", CeremonyCodes.ValidationStatus.Rejected, "No existe una aprobación habilitante de Régimen Interior.");
    }

    private static CeremonyRequirementResult EvaluateTreasury(string? status)
    {
        if (status is TreasuryCodes.RegularityStatus.UpToDate or TreasuryCodes.RegularityStatus.Exempt)
        {
            return new("gran_tesoreria", "Gran Tesorería", CeremonyCodes.ValidationStatus.Approved, "El Taller se encuentra al día para la fecha evaluada.");
        }

        return new("gran_tesoreria", "Gran Tesorería", CeremonyCodes.ValidationStatus.Rejected,
            status is null
                ? "No existe validación de regularidad del Taller en Gran Tesorería."
                : "El Taller no se encuentra al día en Gran Tesorería.");
    }

    private static CeremonyRequirementResult EvaluateHospitalaria(string? status)
    {
        if (status is HospitalariaCodes.RegularityStatus.UpToDate or HospitalariaCodes.RegularityStatus.Exempt)
        {
            return new("gran_hospitalaria", "Gran Hospitalaria", CeremonyCodes.ValidationStatus.Approved, "El Taller se encuentra al día en reposiciones u obligaciones hospitalarias.");
        }

        return new("gran_hospitalaria", "Gran Hospitalaria", CeremonyCodes.ValidationStatus.Rejected,
            status is null
                ? "No existe validación de regularidad del Taller en Gran Hospitalaria."
                : "El Taller presenta reposiciones u obligaciones hospitalarias pendientes.");
    }

    private static CeremonyRequirementResult EvaluateGrandMaster(string? status)
    {
        if (status == CeremonyCodes.ValidationStatus.Approved)
        {
            return new("gran_maestria", "Gran Maestría", CeremonyCodes.ValidationStatus.Approved,
                "Visto bueno de Gran Maestría registrado.");
        }

        if (status is null or CeremonyCodes.ValidationStatus.Pending or CeremonyCodes.ValidationStatus.Observed)
        {
            return new("gran_maestria", "Gran Maestría", CeremonyCodes.ValidationStatus.Observed,
                "El visto bueno de Gran Maestría está pendiente o presenta observaciones.");
        }

        return new("gran_maestria", "Gran Maestría", CeremonyCodes.ValidationStatus.Rejected,
            "Gran Maestría no ha otorgado un visto bueno habilitante para la ceremonia.");
    }

    private static CeremonyRequirementResult EvaluatePublication(CandidatePublicationEvidence? publication)
    {
        if (publication is null)
        {
            return new("publicacion_insinuado", "Publicación del insinuado", CeremonyCodes.ValidationStatus.Rejected,
                "El insinuado no registra una publicación válida.");
        }

        if (publication.Status is not CeremonyCodes.PublicationStatus.Published and not CeremonyCodes.PublicationStatus.Completed)
        {
            return new("publicacion_insinuado", "Publicación del insinuado", CeremonyCodes.ValidationStatus.Rejected,
                "La publicación del insinuado no se encuentra en un estado válido para computar el plazo.");
        }

        if (publication.CompletedDays < publication.RequiredDays)
        {
            return new("publicacion_insinuado", "Publicación del insinuado", CeremonyCodes.ValidationStatus.Rejected,
                $"Se requieren {publication.RequiredDays} días de publicación y se han cumplido {publication.CompletedDays} días válidos.");
        }

        return new("publicacion_insinuado", "Publicación del insinuado", CeremonyCodes.ValidationStatus.Approved,
            $"Cumple {publication.CompletedDays} días válidos de publicación; mínimo exigido: {publication.RequiredDays}.");
    }
}

public sealed record CandidatePublicationEvidence(
    Guid PublicationId,
    string Status,
    int RequiredDays,
    int CompletedDays,
    string RuleCode);

public sealed record CeremonyRequirementResult(
    string Code,
    string Name,
    string Status,
    string Reason);

public sealed record CeremonyEligibilityDecision(
    string Status,
    bool CanAuthorize,
    IReadOnlyList<CeremonyRequirementResult> Requirements);
