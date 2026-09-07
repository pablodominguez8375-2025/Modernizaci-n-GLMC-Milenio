using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy")
            .WithTags("Privacidad y Ley 21.719")
            .RequireAuthorization();

        group.MapPost("/data-subject-requests/{id:guid}/resolve", ResolveDataSubjectRequestAsync);
        group.MapPost("/incidents/{id:guid}/assessment", AssessIncidentAsync);
        group.MapPost("/impact-assessments/{id:guid}/approve", ApproveImpactAssessmentAsync);

        return endpoints;
    }

    private static async Task<IResult> ResolveDataSubjectRequestAsync(
        Guid id,
        ResolveDataSubjectRequestRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var entity = await db.DataSubjectRequests.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "La solicitud de derechos no existe." });
        }

        if (entity.Status == PrivacyCodes.Status.Closed)
        {
            return Results.Conflict(new { message = "La solicitud ya se encuentra cerrada." });
        }

        if (!entity.IdentityVerified)
        {
            return Results.Conflict(new { message = "No puede resolverse la solicitud sin verificación de identidad." });
        }

        if (string.IsNullOrWhiteSpace(request.Resolution))
        {
            return Results.BadRequest(new { message = "La resolución es obligatoria." });
        }

        entity.Resolution = request.Resolution.Trim();
        entity.Grounds = request.Grounds;
        entity.EvidenceReference = request.EvidenceReference ?? entity.EvidenceReference;
        entity.ResponsibleSubject = request.ResponsibleSubject ?? entity.ResponsibleSubject;
        entity.Status = PrivacyCodes.Status.Closed;
        entity.ClosedAtUtc = DateTimeOffset.UtcNow;

        audit.Add(httpContext, "privacy.data_subject_request.resolved", nameof(DataSubjectRequest), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.RequestType, entity.PersonId, entity.ClosedAtUtc });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.ClosedAtUtc });
    }

    private static async Task<IResult> AssessIncidentAsync(
        Guid id,
        AssessPrivacyIncidentRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var entity = await db.PrivacySecurityIncidents.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "El incidente no existe." });
        }

        if (string.IsNullOrWhiteSpace(request.RiskLevel))
        {
            return Results.BadRequest(new { message = "Debe registrarse el nivel de riesgo evaluado." });
        }

        if (request.AuthorityNotifiedAtUtc is not null && !request.NotifyAuthority)
        {
            return Results.BadRequest(new { message = "No puede registrarse una notificación a la autoridad si la decisión de notificar es negativa." });
        }

        if (request.SubjectsNotifiedAtUtc is not null && !request.NotifySubjects)
        {
            return Results.BadRequest(new { message = "No puede registrarse una comunicación a titulares si la decisión de comunicar es negativa." });
        }

        entity.RiskLevel = request.RiskLevel;
        entity.ImmediateMeasures = request.ImmediateMeasures ?? entity.ImmediateMeasures;
        entity.NotifyAuthority = request.NotifyAuthority;
        entity.NotifySubjects = request.NotifySubjects;
        entity.AuthorityNotifiedAtUtc = request.AuthorityNotifiedAtUtc;
        entity.AuthorityReference = request.AuthorityReference;
        entity.SubjectsNotifiedAtUtc = request.SubjectsNotifiedAtUtc;
        entity.CorrectiveActions = request.CorrectiveActions;
        entity.Status = request.CloseIncident ? PrivacyCodes.Status.Closed : PrivacyCodes.Status.UnderReview;

        audit.Add(httpContext, "privacy.incident.assessed", nameof(PrivacySecurityIncident), entity.Id.ToString(), null, AuditResults.Success,
            new
            {
                entity.RiskLevel,
                entity.NotifyAuthority,
                entity.NotifySubjects,
                entity.AuthorityNotifiedAtUtc,
                entity.SubjectsNotifiedAtUtc,
                entity.Status
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.RiskLevel, entity.NotifyAuthority, entity.NotifySubjects });
    }

    private static async Task<IResult> ApproveImpactAssessmentAsync(
        Guid id,
        ApprovePrivacyImpactAssessmentRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var entity = await db.PrivacyImpactAssessments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "La evaluación de impacto no existe." });
        }

        if (entity.Status == PrivacyCodes.Status.Approved)
        {
            return Results.Conflict(new { message = "La evaluación de impacto ya está aprobada." });
        }

        if (entity.HighRisk && string.IsNullOrWhiteSpace(request.ResidualRisk))
        {
            return Results.BadRequest(new { message = "Una EIPD de alto riesgo requiere registrar el riesgo residual." });
        }

        var approver = request.ApprovedBySubject
            ?? httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(approver))
        {
            return Results.BadRequest(new { message = "No fue posible identificar a la autoridad que aprueba la EIPD." });
        }

        entity.ResidualRisk = request.ResidualRisk ?? entity.ResidualRisk;
        entity.Mitigations = request.Mitigations ?? entity.Mitigations;
        entity.EvidenceReference = request.EvidenceReference ?? entity.EvidenceReference;
        entity.ApprovedBySubject = approver;
        entity.ApprovedAtUtc = DateTimeOffset.UtcNow;
        entity.Status = PrivacyCodes.Status.Approved;

        audit.Add(httpContext, "privacy.impact_assessment.approved", nameof(PrivacyImpactAssessment), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.DataProcessingActivityId, entity.HighRisk, entity.RiskLevel, entity.ResidualRisk, entity.ApprovedAtUtc });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.ApprovedBySubject, entity.ApprovedAtUtc });
    }
}

public sealed record ResolveDataSubjectRequestRequest(
    string Resolution,
    string? Grounds,
    string? EvidenceReference,
    string? ResponsibleSubject);

public sealed record AssessPrivacyIncidentRequest(
    string RiskLevel,
    string? ImmediateMeasures,
    bool NotifyAuthority,
    bool NotifySubjects,
    DateTimeOffset? AuthorityNotifiedAtUtc,
    string? AuthorityReference,
    DateTimeOffset? SubjectsNotifiedAtUtc,
    string? CorrectiveActions,
    bool CloseIncident);

public sealed record ApprovePrivacyImpactAssessmentRequest(
    string? ResidualRisk,
    string? Mitigations,
    string? EvidenceReference,
    string? ApprovedBySubject);
