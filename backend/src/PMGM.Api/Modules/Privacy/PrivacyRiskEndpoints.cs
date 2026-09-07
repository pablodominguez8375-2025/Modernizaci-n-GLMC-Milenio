using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyRiskEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyRiskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy")
            .WithTags("Privacidad y Ley 21.719")
            .RequireAuthorization();

        group.MapGet("/incidents", GetIncidentsAsync);
        group.MapPost("/incidents", CreateIncidentAsync);
        group.MapGet("/impact-assessments", GetImpactAssessmentsAsync);
        group.MapPost("/impact-assessments", CreateImpactAssessmentAsync);
        group.MapGet("/retention-policies", GetRetentionPoliciesAsync);
        group.MapPost("/retention-policies", CreateRetentionPolicyAsync);

        return endpoints;
    }

    private static async Task<IResult> GetIncidentsAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var rows = await db.PrivacySecurityIncidents
            .AsNoTracking()
            .OrderByDescending(x => x.DetectedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.DetectedAtUtc,
                x.Source,
                x.Nature,
                x.InvolvesSensitiveData,
                x.EstimatedSubjects,
                x.RiskLevel,
                x.Status,
                x.NotifyAuthority,
                x.NotifySubjects,
                x.AuthorityNotifiedAtUtc,
                x.SubjectsNotifiedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateIncidentAsync(
        CreatePrivacyIncidentRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.Nature))
        {
            return Results.BadRequest(new { message = "Origen y naturaleza del incidente son obligatorios." });
        }

        var entity = new PrivacySecurityIncident
        {
            DetectedAtUtc = request.DetectedAtUtc ?? DateTimeOffset.UtcNow,
            Source = request.Source.Trim(),
            Nature = request.Nature.Trim(),
            DataCategoriesJson = request.DataCategoriesJson,
            InvolvesSensitiveData = request.InvolvesSensitiveData,
            EstimatedSubjects = request.EstimatedSubjects,
            RiskLevel = request.RiskLevel,
            ImmediateMeasures = request.ImmediateMeasures,
            Status = PrivacyCodes.Status.UnderReview,
            NotifyAuthority = request.NotifyAuthority,
            NotifySubjects = request.NotifySubjects
        };

        db.PrivacySecurityIncidents.Add(entity);
        audit.Add(httpContext, "privacy.incident.created", nameof(PrivacySecurityIncident), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.RiskLevel, entity.InvolvesSensitiveData, entity.EstimatedSubjects, entity.NotifyAuthority, entity.NotifySubjects });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/incidents/{entity.Id}", new { entity.Id, entity.Status });
    }

    private static async Task<IResult> GetImpactAssessmentsAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var rows = await db.PrivacyImpactAssessments
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.DataProcessingActivityId,
                processingActivityCode = x.DataProcessingActivity.Code,
                x.RiskLevel,
                x.HighRisk,
                x.Status,
                x.ResidualRisk,
                x.ApprovedBySubject,
                x.ApprovedAtUtc,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateImpactAssessmentAsync(
        CreatePrivacyImpactAssessmentRequest request,
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

        var processingActivity = await db.DataProcessingActivities
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.DataProcessingActivityId, cancellationToken);

        if (processingActivity is null)
        {
            return Results.BadRequest(new { message = "La actividad de tratamiento indicada no existe." });
        }

        if (string.IsNullOrWhiteSpace(request.AssessmentSummary))
        {
            return Results.BadRequest(new { message = "La evaluación debe contener un resumen de análisis." });
        }

        var entity = new PrivacyImpactAssessment
        {
            DataProcessingActivityId = request.DataProcessingActivityId,
            RiskLevel = request.RiskLevel,
            HighRisk = request.HighRisk,
            Status = PrivacyCodes.Status.UnderReview,
            AssessmentSummary = request.AssessmentSummary.Trim(),
            Mitigations = request.Mitigations,
            ResidualRisk = request.ResidualRisk,
            EvidenceReference = request.EvidenceReference
        };

        db.PrivacyImpactAssessments.Add(entity);
        audit.Add(httpContext, "privacy.impact_assessment.created", nameof(PrivacyImpactAssessment), entity.Id.ToString(), null, AuditResults.Success,
            new { processingActivity.Code, entity.RiskLevel, entity.HighRisk });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/impact-assessments/{entity.Id}", new { entity.Id, entity.Status });
    }

    private static async Task<IResult> GetRetentionPoliciesAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var rows = await db.DataRetentionPolicies
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ThenByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateRetentionPolicyAsync(
        CreateRetentionPolicyRequest request,
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

        if (!PrivacyCodes.RetentionAction.IsValid(request.ExpirationAction))
        {
            return Results.BadRequest(new { message = "La acción de expiración no es válida." });
        }

        if (request.RetentionDays is null && string.IsNullOrWhiteSpace(request.ExpirationEvent))
        {
            return Results.BadRequest(new { message = "Debe indicarse un plazo de conservación o un evento de expiración." });
        }

        var duplicate = await db.DataRetentionPolicies.AnyAsync(x =>
            x.Code == request.Code && x.EffectiveFrom == request.EffectiveFrom,
            cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Ya existe una política con ese código y fecha de vigencia." });
        }

        var entity = new DataRetentionPolicy
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            DataCategory = request.DataCategory.Trim(),
            Purpose = request.Purpose.Trim(),
            LegalBasis = request.LegalBasis.Trim(),
            RetentionDays = request.RetentionDays,
            ExpirationEvent = request.ExpirationEvent,
            ExpirationAction = request.ExpirationAction,
            AllowsLegalHold = request.AllowsLegalHold,
            Status = request.Status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo
        };

        db.DataRetentionPolicies.Add(entity);
        audit.Add(httpContext, "privacy.retention_policy.created", nameof(DataRetentionPolicy), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.Code, entity.DataCategory, entity.RetentionDays, entity.ExpirationEvent, entity.ExpirationAction });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/retention-policies/{entity.Id}", new { entity.Id });
    }
}

public sealed record CreatePrivacyIncidentRequest(
    DateTimeOffset? DetectedAtUtc,
    string Source,
    string Nature,
    string DataCategoriesJson,
    bool InvolvesSensitiveData,
    int? EstimatedSubjects,
    string RiskLevel,
    string? ImmediateMeasures,
    bool NotifyAuthority,
    bool NotifySubjects);

public sealed record CreatePrivacyImpactAssessmentRequest(
    Guid DataProcessingActivityId,
    string RiskLevel,
    bool HighRisk,
    string AssessmentSummary,
    string? Mitigations,
    string? ResidualRisk,
    string? EvidenceReference);

public sealed record CreateRetentionPolicyRequest(
    string Code,
    string Name,
    string DataCategory,
    string Purpose,
    string LegalBasis,
    int? RetentionDays,
    string? ExpirationEvent,
    string ExpirationAction,
    bool AllowsLegalHold,
    string Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
