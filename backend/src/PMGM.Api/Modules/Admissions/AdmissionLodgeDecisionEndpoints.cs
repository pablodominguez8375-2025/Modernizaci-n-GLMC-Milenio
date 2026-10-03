using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionLodgeDecisionEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionLodgeDecisionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admisiones")
            .WithTags("Afiliaciones e incorporaciones - decisiones del Taller")
            .RequireAuthorization();

        group.MapPost("/expedientes/{caseId:guid}/decisiones/presentacion-primer-grado", RecordFirstDegreePresentationAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/tercer-grado", RecordThirdDegreeDecisionAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/balotaje-primer-grado", RecordFirstDegreeBallotAsync);
        group.MapGet("/expedientes/{caseId:guid}/habilitacion-procedimiento", GetProcedureEligibilityAsync);

        return endpoints;
    }

    private static Task<IResult> RecordFirstDegreePresentationAsync(Guid caseId, AdmissionLodgeDecisionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
        => RecordLodgeDecisionAsync(caseId, AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreePresentation,
            "admission.lodge.first_degree_presentation.recorded", request, context, admissionsDb, coreDb, access, audit, ct);

    private static Task<IResult> RecordThirdDegreeDecisionAsync(
        Guid caseId,
        AdmissionLodgeDecisionRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
        => RecordLodgeDecisionAsync(
            caseId,
            AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval,
            "admission.lodge.third_degree_decision.recorded",
            request,
            httpContext,
            admissionsDb,
            coreDb,
            access,
            audit,
            cancellationToken);

    private static Task<IResult> RecordFirstDegreeBallotAsync(
        Guid caseId,
        AdmissionLodgeDecisionRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
        => RecordLodgeDecisionAsync(
            caseId,
            AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreeBallot,
            "admission.lodge.first_degree_ballot.recorded",
            request,
            httpContext,
            admissionsDb,
            coreDb,
            access,
            audit,
            cancellationToken);

    private static async Task<IResult> RecordLodgeDecisionAsync(
        Guid caseId,
        string decisionType,
        string auditAction,
        AdmissionLodgeDecisionRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (request.Status is not CeremonyCodes.ValidationStatus.Approved and not CeremonyCodes.ValidationStatus.Rejected)
            return Results.BadRequest(new { message = "La decisión del Taller debe registrarse como aprobada o rechazada." });
        if (request.AsOfDate > ChileToday())
            return Results.BadRequest(new { message = "La fecha de la decisión no puede estar en el futuro." });
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe registrar la referencia del acta que respalda la decisión." });

        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya se encuentra resuelto." });

        var decision = new AdmissionDecision
        {
            AdmissionCaseId = caseId,
            DecisionType = decisionType,
            Status = request.Status,
            AsOfDate = request.AsOfDate,
            SourceReference = request.SourceReference.Trim(),
            Notes = Normalize(request.Notes),
            RecordedBySubject = GetSubject(httpContext.User)
        };

        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(cancellationToken);

        audit.Add(
            httpContext,
            auditAction,
            nameof(AdmissionDecision),
            decision.Id.ToString(),
            admissionCase.OrganizationId,
            decision.Status == CeremonyCodes.ValidationStatus.Approved ? AuditResults.Success : AuditResults.Rejected,
            new { decision.DecisionType, decision.Status, decision.AsOfDate });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            decision.Id,
            decision.AdmissionCaseId,
            decision.DecisionType,
            decision.Status,
            decision.AsOfDate,
            decision.SourceReference,
            decision.RecordedAtUtc
        });
    }

    private static async Task<IResult> GetProcedureEligibilityAsync(
        Guid caseId,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking()
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (admissionCase is null) return Results.NotFound();

        if (!access.CanReadOrganization(httpContext.User, admissionCase.OrganizationId) &&
            !access.CanEvaluateCeremonies(httpContext.User))
            return Results.Forbid();

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);
        var eligibility = projection.Decision;

        return Results.Ok(new
        {
            caseId,
            admissionCase.AdmissionType,
            status = eligibility.Status,
            canProceedToCeremonyRequest = eligibility.CanProceed,
            eligibility.Requirements,
            actions = new
            {
                canManageLodge = access.CanManageLodgeSecretariat(httpContext.User, admissionCase.OrganizationId),
                canReviewSignature = access.CanManageGrandSecretariat(httpContext.User),
                canReviewArticle23 = access.CanValidateCeremonyInternalAffairs(httpContext.User),
                canProvideGrandMasterDecision = access.CanProvideGrandMasterApproval(httpContext.User)
            },
            evidence = new
            {
                withdrawalLetterId = projection.WithdrawalLetterId,
                thirdDegreeDecisionId = projection.ThirdDegreeDecisionId,
                firstDegreeBallotDecisionId = projection.FirstDegreeBallotDecisionId,
                grandMasterSpecialAcceptanceDecisionId = projection.GrandMasterSpecialAcceptanceDecisionId
            }
        });
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

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly ChileToday()
    {
        var chile = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chile.DateTime);
    }

    private static DateOnly ChileDate(DateTimeOffset instant)
    {
        var chile = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(instant, "America/Santiago");
        return DateOnly.FromDateTime(chile.DateTime);
    }
}

public sealed record AdmissionLodgeDecisionRequest(
    string Status,
    DateOnly AsOfDate,
    string SourceReference,
    string? Notes);
