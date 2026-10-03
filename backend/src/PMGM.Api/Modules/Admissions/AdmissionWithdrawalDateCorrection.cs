using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionWithdrawalDateCorrection
{
    public const string DecisionPrefix = "withdrawal_letter_date_correction:";

    public static bool IsDocumentReview(string type) =>
        type.StartsWith(AdmissionWorkflowCodes.DecisionType.EvidenceReviewPrefix, StringComparison.Ordinal) ||
        type == AdmissionWorkflowCodes.DecisionType.WithdrawalLetterHandwrittenSignature ||
        type.StartsWith(AdmissionWorkflowCodes.DecisionType.WithdrawalLetterHandwrittenSignature + ":", StringComparison.Ordinal) ||
        type.StartsWith(DecisionPrefix, StringComparison.Ordinal);

    public static string? CorrectedMode(AdmissionCase entity, Guid evidenceId, DateOnly today)
    {
        var letter = AdmissionWithdrawalEvidencePolicy.CurrentLetter(entity);
        if (entity.AdmissionType != CeremonyCodes.Type.Affiliation ||
            entity.Status is not AdmissionWorkflowCodes.CaseStatus.UnderReview and not AdmissionWorkflowCodes.CaseStatus.Observed ||
            entity.Decisions.Any(x => !IsDocumentReview(x.DecisionType)) ||
            letter is null || letter.Id != evidenceId || letter.DocumentVersionId is null ||
            letter.ReviewStatus != CeremonyCodes.ValidationStatus.Approved || letter.ReviewedAtUtc is null ||
            letter.EvidenceDate is not { } granted || granted > today) return null;
        return WithdrawalLetterPolicy.ModeAtCreation(granted, AdmissionWithdrawalEvidencePolicy.ChileDate(entity.CreatedAtUtc));
    }

    public static async Task<IResult> CorrectAsync(Guid caseId, WithdrawalLetterDateCorrectionRequest request,
        HttpContext httpContext, PmgmDbContext coreDb, AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-store";
        if (!access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();
        var today = AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow);
        if (request.EvidenceId == Guid.Empty || request.AsOfDate > today ||
            string.IsNullOrWhiteSpace(request.SourceReference) || request.SourceReference.Length > 500 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 4000)
            return Results.BadRequest(new { message = "Indique carta concreta, fecha de revisión no futura, fuente y motivo de corrección." });

        admissionsDb.Database.SetDbConnection(coreDb.Database.GetDbConnection());
        await using var transaction = await coreDb.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await admissionsDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
        try
        {
            var entity = await admissionsDb.AdmissionCases.Include(x => x.Evidence).Include(x => x.Decisions)
                .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
            if (entity is null) return Results.NotFound();
            var mode = CorrectedMode(entity, request.EvidenceId, today);
            var letter = AdmissionWithdrawalEvidencePolicy.CurrentLetter(entity);
            if (mode is null || request.AsOfDate < letter!.EvidenceDate ||
                await coreDb.CeremonyRequests.AnyAsync(x => x.AdmissionCaseId == caseId, cancellationToken))
                return Results.Conflict(new { message = "La corrección exige la carta vigente aprobada, anterior al alta, y un expediente sin decisiones del procedimiento ni ceremonia." });
            if (entity.WithdrawalLetterGrantedDate == letter!.EvidenceDate && entity.AffiliationMode == mode)
                return Results.Conflict(new { message = "La fecha y modalidad ya coinciden con la carta; no hay corrección que registrar." });
            var previousDate = entity.WithdrawalLetterGrantedDate;
            var previousMode = entity.AffiliationMode;
            entity.WithdrawalLetterGrantedDate = letter.EvidenceDate;
            entity.AffiliationMode = mode;
            var decision = new AdmissionDecision
            {
                AdmissionCaseId = caseId, DecisionType = DecisionPrefix + letter.Id.ToString("D"),
                Status = CeremonyCodes.ValidationStatus.Approved, AsOfDate = request.AsOfDate,
                SourceReference = request.SourceReference.Trim(), Notes = request.Reason.Trim(),
                RecordedBySubject = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.Identity?.Name ?? "unknown"
            };
            admissionsDb.Add(decision);
            await admissionsDb.SaveChangesAsync(cancellationToken);
            audit.Add(httpContext, "admission.withdrawal_letter.date_corrected", nameof(AdmissionCase), entity.Id.ToString(),
                entity.OrganizationId, AuditResults.Success, new { previousDate, previousMode,
                    grantedDate = entity.WithdrawalLetterGrantedDate, affiliationMode = mode, evidenceId = letter.Id,
                    letter.DocumentVersionId, decisionId = decision.Id, request.AsOfDate });
            await coreDb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new { entity.Id, entity.WithdrawalLetterGrantedDate, entity.AffiliationMode,
                evidenceId = letter.Id, decisionId = decision.Id, signatureReviewRequired = true });
        }
        catch (Exception ex) when (IsSerializationConflict(ex))
        {
            return Results.Conflict(new { message = "El expediente cambió durante la revisión. Actualice sus antecedentes y vuelva a comprobarlos." });
        }
    }
    private static bool IsSerializationConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: "40001" }) return true;
        return false;
    }
}
public sealed record WithdrawalLetterDateCorrectionRequest(Guid EvidenceId, DateOnly AsOfDate, string SourceReference, string Reason);
