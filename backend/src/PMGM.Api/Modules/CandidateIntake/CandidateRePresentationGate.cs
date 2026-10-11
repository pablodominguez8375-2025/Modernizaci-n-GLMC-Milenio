using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// Compuerta común de reinsinuación para todos los endpoints que crean Iniciaciones.
/// Aplica el último rechazo reglamentario de cualquier Taller, no sólo el Taller solicitante.
/// La constancia se registra en el nuevo expediente sin borrar los eventos anteriores.
/// </summary>
public static class CandidateRePresentationGate
{
    public sealed record PriorRejection(Guid ValidationId, Guid CeremonyRequestId, DateOnly RejectionDate);

    public sealed record Decision(bool CanProceed, bool IsRePresentation, string Code, string Reason,
        PriorRejection? PriorRejection, string? SourceReference);

    public static async Task<PriorRejection?> FindLatestAsync(PmgmDbContext db, Guid personId,
        CancellationToken cancellationToken)
        => await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequest.CandidatePersonId == personId &&
                        (x.ValidationType == CeremonyCodes.ValidationType.CandidateThirdDegreeReview ||
                         x.ValidationType == CeremonyCodes.ValidationType.CandidateFinalBallot) &&
                        x.Status == CeremonyCodes.ValidationStatus.Rejected)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new PriorRejection(x.Id, x.CeremonyRequestId, x.AsOfDate))
            .FirstOrDefaultAsync(cancellationToken);

    public static Decision Evaluate(PriorRejection? prior, DateOnly presentationDate,
        bool rejectionCausesRemedied, string? evidenceReference)
    {
        var reference = evidenceReference?.Trim();
        if (prior is null)
        {
            if (rejectionCausesRemedied || !string.IsNullOrWhiteSpace(reference))
                return new(false, false, "representation.no_prior_rejection",
                    "La constancia de subsanación sólo corresponde a una nueva presentación después de un rechazo registrado.",
                    null, null);
            return new(true, false, "representation.not_applicable", "No se registra rechazo anterior.",
                null, null);
        }

        var decision = CandidateIntakeWorkflowPolicy.EvaluateRePresentation(
            prior.RejectionDate, presentationDate, rejectionCausesRemedied);
        if (!decision.CanProceed)
            return new(false, true, decision.Code, decision.Reason, prior, null);

        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 240)
            return new(false, true, "representation.evidence_required",
                "Debe indicar una referencia documental de la subsanación (máximo 240 caracteres).",
                prior, null);

        return new(true, true, "representation.allowed",
            "Cumple un año desde el rechazo y se registró una constancia documental de subsanación.",
            prior, reference);
    }

    public static CeremonyValidation? AddApproval(PmgmDbContext db, Guid newCeremonyRequestId,
        DateOnly presentationDate, Decision decision)
    {
        if (!decision.CanProceed || !decision.IsRePresentation || decision.PriorRejection is null ||
            string.IsNullOrWhiteSpace(decision.SourceReference))
            return null;

        var validation = new CeremonyValidation
        {
            CeremonyRequestId = newCeremonyRequestId,
            ValidationType = CeremonyCodes.ValidationType.CandidateRepresentationReview,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = presentationDate,
            SourceReference = decision.SourceReference,
            Notes = $"Constancia de subsanación declarada por Secretaría; antecedente de rechazo #{decision.PriorRejection.ValidationId} de fecha {decision.PriorRejection.RejectionDate:yyyy-MM-dd}. Requiere evaluación institucional posterior."
        };
        db.CeremonyValidations.Add(validation);
        return validation;
    }
}
