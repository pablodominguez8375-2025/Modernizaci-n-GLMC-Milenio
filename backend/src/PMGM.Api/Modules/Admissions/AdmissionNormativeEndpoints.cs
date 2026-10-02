using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

/// <summary>
/// Residual controls from historical PR #116, adapted to the current dev model.
/// Decisions are recorded in the existing admission_decisions ledger; no second
/// admissions model or unapproved tariff/authority rule is introduced here.
/// </summary>
public static class AdmissionNormativeEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionNormativeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admisiones")
            .WithTags("Afiliaciones e incorporaciones - controles reglamentarios")
            .RequireAuthorization();

        group.MapPost("/expedientes/{caseId:guid}/revision-articulo-2-3", RecordArticle23ReviewAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/indulto-gran-maestria", RecordGrandMasterPardonAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/reconocimiento-regularidad", RecordGrandMasterRegularityRecognitionAsync);
        return endpoints;
    }

    private static Task<IResult> RecordGrandMasterPardonAsync(Guid caseId, AdmissionAuthorityDecisionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
        => RecordAuthorityDecisionAsync(caseId, AdmissionWorkflowCodes.DecisionType.GrandMasterPardon,
            "admission.grand_master.pardon.recorded", request, context, admissionsDb, coreDb, access, audit, ct,
            requireIncorporation: false);

    private static Task<IResult> RecordGrandMasterRegularityRecognitionAsync(Guid caseId, AdmissionAuthorityDecisionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
        => RecordAuthorityDecisionAsync(caseId, AdmissionWorkflowCodes.DecisionType.GrandMasterRegularityRecognition,
            "admission.grand_master.regularity_recognition.recorded", request, context, admissionsDb, coreDb, access, audit, ct,
            requireIncorporation: true);

    private static async Task<IResult> RecordArticle23ReviewAsync(Guid caseId, Article23ReviewRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanValidateCeremonyInternalAffairs(context.User)) return Results.Forbid();
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La revisión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe indicar la fuente que respalda la revisión del art. 2.3." });
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });
        var blocked = request.HasRayamiento || request.HasTribunalForcedWithdrawal;
        var decision = NewDecision(caseId, AdmissionWorkflowCodes.DecisionType.Article23Review,
            blocked ? CeremonyCodes.ValidationStatus.Rejected : CeremonyCodes.ValidationStatus.Approved,
            request.AsOfDate, request.SourceReference, JsonSerializer.Serialize(new { request.HasRayamiento, request.HasTribunalForcedWithdrawal, request.Notes }),
            Subject(context.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.article_2_3.reviewed", nameof(AdmissionDecision), decision.Id.ToString(), admissionCase.OrganizationId,
            blocked ? AuditResults.Rejected : AuditResults.Success, new { request.HasRayamiento, request.HasTribunalForcedWithdrawal, decision.Status });
        await coreDb.SaveChangesAsync(ct);
        return Results.Ok(ToDto(decision));
    }

    private static async Task<IResult> RecordAuthorityDecisionAsync(Guid caseId, string decisionType, string auditAction,
        AdmissionAuthorityDecisionRequest request, HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct, bool requireIncorporation)
    {
        if (!access.CanProvideGrandMasterApproval(context.User)) return Results.Forbid();
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La decisión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe registrar la resolución o fuente institucional." });
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (requireIncorporation && admissionCase.AdmissionType != CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "El reconocimiento de regularidad sólo aplica a incorporaciones." });
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });
        var decision = NewDecision(caseId, decisionType,
            request.Approved ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected,
            request.AsOfDate, request.SourceReference, request.Notes, Subject(context.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, auditAction, nameof(AdmissionDecision), decision.Id.ToString(), admissionCase.OrganizationId,
            request.Approved ? AuditResults.Success : AuditResults.Rejected, new { decision.DecisionType, decision.Status });
        await coreDb.SaveChangesAsync(ct);
        return Results.Ok(ToDto(decision));
    }

    private static AdmissionDecision NewDecision(Guid caseId, string type, string status, DateOnly asOfDate, string source, string? notes, string subject)
        => new() { AdmissionCaseId = caseId, DecisionType = type, Status = status, AsOfDate = asOfDate,
            SourceReference = source.Trim(), Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), RecordedBySubject = subject };

    private static object ToDto(AdmissionDecision decision) => new { decision.Id, decision.AdmissionCaseId, decision.DecisionType,
        decision.Status, decision.AsOfDate, decision.SourceReference, decision.Notes, decision.RecordedBySubject, decision.RecordedAtUtc };

    private static string Subject(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private static DateOnly ChileToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
}

public sealed record Article23ReviewRequest(bool HasRayamiento, bool HasTribunalForcedWithdrawal, DateOnly AsOfDate, string SourceReference, string? Notes);
public sealed record AdmissionAuthorityDecisionRequest(bool Approved, DateOnly AsOfDate, string SourceReference, string? Notes);