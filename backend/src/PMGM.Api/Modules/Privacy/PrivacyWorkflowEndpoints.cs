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
        group.MapGet("/incidents/{id:guid}/workflow", GetIncidentWorkflowAsync);
        group.MapPost("/incidents/{id:guid}/assessment", AssessIncidentAsync);
        group.MapPost("/incidents/{id:guid}/notification-decisions", SetIncidentNotificationDecisionAsync);
        group.MapPost("/incidents/{id:guid}/communications", RegisterIncidentCommunicationAsync);
        group.MapPost("/incidents/{id:guid}/corrective-actions", SetIncidentCorrectiveActionsAsync);
        group.MapPost("/incidents/{id:guid}/close", CloseIncidentAsync);
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

        audit.Add(
            httpContext,
            "privacy.data_subject_request.resolved",
            nameof(DataSubjectRequest),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new { entity.RequestType, entity.ClosedAtUtc });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.ClosedAtUtc });
    }

    private static async Task<IResult> GetIncidentWorkflowAsync(
        Guid id,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var incident = await db.PrivacySecurityIncidents
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (incident is null)
        {
            return Results.NotFound(new { message = "El incidente no existe." });
        }

        var entityId = id.ToString();
        var timeline = await db.AuditEvents
            .AsNoTracking()
            .Where(x =>
                x.EntityType == nameof(PrivacySecurityIncident) &&
                x.EntityId == entityId &&
                x.Action.StartsWith("privacy.incident."))
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => new
            {
                x.OccurredAtUtc,
                x.Action,
                x.ActorDisplayName,
                x.Result,
                x.CorrelationId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            incident.Id,
            incident.DetectedAtUtc,
            incident.RiskLevel,
            incident.Status,
            assessment = new
            {
                incident.AssessmentCompletedAtUtc,
                incident.AssessmentSummary,
                incident.ImmediateMeasures
            },
            authority = new
            {
                incident.AuthorityDecision,
                incident.AuthorityDecisionAtUtc,
                incident.AuthorityDecisionReason,
                incident.NotifyAuthority,
                incident.AuthorityNotifiedAtUtc,
                incident.AuthorityNotificationChannel,
                incident.AuthorityReference
            },
            subjects = new
            {
                incident.SubjectsDecision,
                incident.SubjectsDecisionAtUtc,
                incident.SubjectsDecisionReason,
                incident.NotifySubjects,
                incident.SubjectsNotifiedAtUtc,
                incident.SubjectsNotificationChannel,
                incident.SubjectsNotificationReference
            },
            incident.CorrectiveActions,
            incident.ClosedAtUtc,
            incident.ClosedBySubject,
            timeline
        });
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

        if (entity.Status == PrivacyIncidentCodes.Status.Closed)
        {
            return Results.Conflict(new { message = "El incidente ya se encuentra cerrado." });
        }

        if (string.IsNullOrWhiteSpace(request.RiskLevel) || string.IsNullOrWhiteSpace(request.AssessmentSummary))
        {
            return Results.BadRequest(new { message = "Deben registrarse el nivel de riesgo y el resumen de la evaluación." });
        }

        entity.RiskLevel = request.RiskLevel.Trim();
        entity.ImmediateMeasures = request.ImmediateMeasures ?? entity.ImmediateMeasures;
        entity.AssessmentSummary = request.AssessmentSummary.Trim();
        entity.AssessmentCompletedAtUtc = DateTimeOffset.UtcNow;
        entity.Status = PrivacyIncidentCodes.Status.Assessed;

        audit.Add(
            httpContext,
            "privacy.incident.assessed",
            nameof(PrivacySecurityIncident),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                entity.RiskLevel,
                entity.AssessmentCompletedAtUtc,
                entity.Status
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            entity.Id,
            entity.Status,
            entity.RiskLevel,
            entity.AssessmentCompletedAtUtc
        });
    }

    private static async Task<IResult> SetIncidentNotificationDecisionAsync(
        Guid id,
        IncidentNotificationDecisionRequest request,
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

        if (!PrivacyIncidentCodes.NotificationTarget.IsValid(request.Target) ||
            !PrivacyIncidentCodes.NotificationDecision.IsValid(request.Decision))
        {
            return Results.BadRequest(new { message = "El destino o la decisión de notificación no son válidos." });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { message = "La decisión debe incluir su fundamento." });
        }

        var entity = await db.PrivacySecurityIncidents.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "El incidente no existe." });
        }

        if (entity.Status == PrivacyIncidentCodes.Status.Closed)
        {
            return Results.Conflict(new { message = "El incidente ya se encuentra cerrado." });
        }

        if (entity.AssessmentCompletedAtUtc is null)
        {
            return Results.Conflict(new { message = "Debe completarse la evaluación de riesgo antes de decidir las notificaciones." });
        }

        var now = DateTimeOffset.UtcNow;
        var notify = request.Decision == PrivacyIncidentCodes.NotificationDecision.Notify;

        if (request.Target == PrivacyIncidentCodes.NotificationTarget.Authority)
        {
            if (entity.AuthorityNotifiedAtUtc is not null && !notify)
            {
                return Results.Conflict(new { message = "Ya existe una notificación a la autoridad; la decisión no puede revertirse a no notificar." });
            }

            entity.AuthorityDecision = request.Decision;
            entity.AuthorityDecisionAtUtc = now;
            entity.AuthorityDecisionReason = request.Reason.Trim();
            entity.NotifyAuthority = notify;
        }
        else
        {
            if (entity.SubjectsNotifiedAtUtc is not null && !notify)
            {
                return Results.Conflict(new { message = "Ya existe comunicación a titulares; la decisión no puede revertirse a no comunicar." });
            }

            entity.SubjectsDecision = request.Decision;
            entity.SubjectsDecisionAtUtc = now;
            entity.SubjectsDecisionReason = request.Reason.Trim();
            entity.NotifySubjects = notify;
        }

        var pendingCommunication =
            (entity.AuthorityDecision == PrivacyIncidentCodes.NotificationDecision.Notify && entity.AuthorityNotifiedAtUtc is null) ||
            (entity.SubjectsDecision == PrivacyIncidentCodes.NotificationDecision.Notify && entity.SubjectsNotifiedAtUtc is null);

        entity.Status = pendingCommunication
            ? PrivacyIncidentCodes.Status.NotificationPending
            : string.IsNullOrWhiteSpace(entity.CorrectiveActions)
                ? PrivacyIncidentCodes.Status.Assessed
                : PrivacyIncidentCodes.Status.Mitigating;

        audit.Add(
            httpContext,
            "privacy.incident.notification_decision.recorded",
            nameof(PrivacySecurityIncident),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                request.Target,
                request.Decision,
                decidedAtUtc = now,
                entity.Status
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            entity.Id,
            request.Target,
            request.Decision,
            decidedAtUtc = now,
            entity.Status
        });
    }

    private static async Task<IResult> RegisterIncidentCommunicationAsync(
        Guid id,
        IncidentCommunicationRequest request,
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

        if (!PrivacyIncidentCodes.NotificationTarget.IsValid(request.Target))
        {
            return Results.BadRequest(new { message = "El destino de la comunicación no es válido." });
        }

        if (string.IsNullOrWhiteSpace(request.Channel) || string.IsNullOrWhiteSpace(request.Reference))
        {
            return Results.BadRequest(new { message = "Canal y referencia de evidencia son obligatorios." });
        }

        var entity = await db.PrivacySecurityIncidents.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "El incidente no existe." });
        }

        if (entity.Status == PrivacyIncidentCodes.Status.Closed)
        {
            return Results.Conflict(new { message = "El incidente ya se encuentra cerrado." });
        }

        var sentAt = request.SentAtUtc ?? DateTimeOffset.UtcNow;

        if (request.Target == PrivacyIncidentCodes.NotificationTarget.Authority)
        {
            if (entity.AuthorityDecision != PrivacyIncidentCodes.NotificationDecision.Notify)
            {
                return Results.Conflict(new { message = "No existe una decisión vigente de notificar a la autoridad." });
            }

            if (entity.AuthorityNotifiedAtUtc is not null)
            {
                return Results.Conflict(new { message = "La notificación a la autoridad ya fue registrada." });
            }

            entity.AuthorityNotifiedAtUtc = sentAt;
            entity.AuthorityNotificationChannel = request.Channel.Trim();
            entity.AuthorityReference = request.Reference.Trim();
        }
        else
        {
            if (entity.SubjectsDecision != PrivacyIncidentCodes.NotificationDecision.Notify)
            {
                return Results.Conflict(new { message = "No existe una decisión vigente de comunicar a los titulares." });
            }

            if (entity.SubjectsNotifiedAtUtc is not null)
            {
                return Results.Conflict(new { message = "La comunicación a titulares ya fue registrada." });
            }

            entity.SubjectsNotifiedAtUtc = sentAt;
            entity.SubjectsNotificationChannel = request.Channel.Trim();
            entity.SubjectsNotificationReference = request.Reference.Trim();
        }

        entity.Status = PrivacyIncidentCodes.Status.Mitigating;

        audit.Add(
            httpContext,
            "privacy.incident.communication.recorded",
            nameof(PrivacySecurityIncident),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                request.Target,
                sentAtUtc = sentAt,
                channel = request.Channel.Trim(),
                entity.Status
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, request.Target, sentAtUtc = sentAt, entity.Status });
    }

    private static async Task<IResult> SetIncidentCorrectiveActionsAsync(
        Guid id,
        IncidentCorrectiveActionsRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Actions))
        {
            return Results.BadRequest(new { message = "Las medidas correctivas son obligatorias." });
        }

        var entity = await db.PrivacySecurityIncidents.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "El incidente no existe." });
        }

        if (entity.Status == PrivacyIncidentCodes.Status.Closed)
        {
            return Results.Conflict(new { message = "El incidente ya se encuentra cerrado." });
        }

        entity.CorrectiveActions = request.Actions.Trim();
        entity.Status = PrivacyIncidentCodes.Status.Mitigating;

        audit.Add(
            httpContext,
            "privacy.incident.corrective_actions.recorded",
            nameof(PrivacySecurityIncident),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new { entity.Status });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status });
    }

    private static async Task<IResult> CloseIncidentAsync(
        Guid id,
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

        if (entity.Status == PrivacyIncidentCodes.Status.Closed)
        {
            return Results.Conflict(new { message = "El incidente ya se encuentra cerrado." });
        }

        var closure = PrivacyIncidentWorkflowPolicy.EvaluateClosure(entity);
        if (!closure.CanClose)
        {
            return Results.Conflict(new
            {
                message = "El incidente todavía no cumple las condiciones de cierre.",
                blockingReasons = closure.BlockingReasons
            });
        }

        var subject = ResolveSubject(httpContext.User);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.BadRequest(new { message = "No fue posible identificar al usuario que cierra el incidente." });
        }

        entity.Status = PrivacyIncidentCodes.Status.Closed;
        entity.ClosedAtUtc = DateTimeOffset.UtcNow;
        entity.ClosedBySubject = subject;

        audit.Add(
            httpContext,
            "privacy.incident.closed",
            nameof(PrivacySecurityIncident),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                entity.RiskLevel,
                entity.AuthorityDecision,
                entity.SubjectsDecision,
                entity.ClosedAtUtc,
                entity.Status
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.ClosedAtUtc });
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

        audit.Add(
            httpContext,
            "privacy.impact_assessment.approved",
            nameof(PrivacyImpactAssessment),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                entity.DataProcessingActivityId,
                entity.HighRisk,
                entity.RiskLevel,
                entity.ApprovedAtUtc
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.ApprovedBySubject, entity.ApprovedAtUtc });
    }

    private static string? ResolveSubject(ClaimsPrincipal principal)
        => principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.Identity?.Name;
}

public sealed record ResolveDataSubjectRequestRequest(
    string Resolution,
    string? Grounds,
    string? EvidenceReference,
    string? ResponsibleSubject);

public sealed record AssessPrivacyIncidentRequest(
    string RiskLevel,
    string AssessmentSummary,
    string? ImmediateMeasures);

public sealed record IncidentNotificationDecisionRequest(
    string Target,
    string Decision,
    string Reason);

public sealed record IncidentCommunicationRequest(
    string Target,
    DateTimeOffset? SentAtUtc,
    string Channel,
    string Reference);

public sealed record IncidentCorrectiveActionsRequest(string Actions);

public sealed record ApprovePrivacyImpactAssessmentRequest(
    string? ResidualRisk,
    string? Mitigations,
    string? EvidenceReference,
    string? ApprovedBySubject);
