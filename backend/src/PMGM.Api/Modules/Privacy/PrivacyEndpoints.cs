using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy")
            .WithTags("Privacidad y Ley 21.719")
            .RequireAuthorization();

        group.MapGet("/dashboard", GetDashboardAsync);
        group.MapGet("/processing-activities", GetProcessingActivitiesAsync);
        group.MapPost("/processing-activities", CreateProcessingActivityAsync);
        group.MapGet("/data-subject-requests", GetDataSubjectRequestsAsync);
        group.MapPost("/data-subject-requests", CreateDataSubjectRequestAsync);

        return endpoints;
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var openRequests = await db.DataSubjectRequests
            .AsNoTracking()
            .CountAsync(x => x.Status != PrivacyCodes.Status.Closed, cancellationToken);

        var overdueRequests = await db.DataSubjectRequests
            .AsNoTracking()
            .CountAsync(x => x.Status != PrivacyCodes.Status.Closed && x.DueDate != null && x.DueDate < today, cancellationToken);

        var highRiskActivities = await db.DataProcessingActivities
            .AsNoTracking()
            .CountAsync(x => x.ContainsSensitiveData && x.Status == PrivacyCodes.Status.Active, cancellationToken);

        var openIncidents = await db.PrivacySecurityIncidents
            .AsNoTracking()
            .CountAsync(x => x.Status != PrivacyCodes.Status.Closed, cancellationToken);

        var pendingImpactAssessments = await db.PrivacyImpactAssessments
            .AsNoTracking()
            .CountAsync(x => x.HighRisk && x.Status != PrivacyCodes.Status.Approved, cancellationToken);

        return Results.Ok(new
        {
            openRequests,
            overdueRequests,
            activeSensitiveProcessingActivities = highRiskActivities,
            openIncidents,
            pendingHighRiskImpactAssessments = pendingImpactAssessments
        });
    }

    private static async Task<IResult> GetProcessingActivitiesAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var rows = await db.DataProcessingActivities
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ThenByDescending(x => x.EffectiveFrom)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Module,
                x.Purpose,
                x.LawfulBasis,
                x.ContainsSensitiveData,
                x.HasInternationalTransfer,
                x.RetentionPolicyId,
                x.InternalOwner,
                x.Status,
                x.EffectiveFrom,
                x.EffectiveTo
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateProcessingActivityAsync(
        CreateProcessingActivityRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Code) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Module) ||
            string.IsNullOrWhiteSpace(request.Purpose) ||
            string.IsNullOrWhiteSpace(request.LawfulBasis))
        {
            return Results.BadRequest(new { message = "Código, nombre, módulo, finalidad y base jurídica son obligatorios." });
        }

        var duplicate = await db.DataProcessingActivities.AnyAsync(x =>
            x.Code == request.Code && x.EffectiveFrom == request.EffectiveFrom,
            cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Ya existe una actividad de tratamiento con ese código y vigencia." });
        }

        if (request.RetentionPolicyId is not null &&
            !await db.DataRetentionPolicies.AnyAsync(x => x.Id == request.RetentionPolicyId.Value, cancellationToken))
        {
            return Results.BadRequest(new { message = "La política de conservación indicada no existe." });
        }

        var entity = new DataProcessingActivity
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Module = request.Module.Trim(),
            Purpose = request.Purpose.Trim(),
            LawfulBasis = request.LawfulBasis.Trim(),
            DataCategoriesJson = request.DataCategoriesJson,
            SubjectCategoriesJson = request.SubjectCategoriesJson,
            ContainsSensitiveData = request.ContainsSensitiveData,
            RecipientsJson = request.RecipientsJson,
            HasInternationalTransfer = request.HasInternationalTransfer,
            RetentionPolicyId = request.RetentionPolicyId,
            InternalOwner = request.InternalOwner,
            Status = request.Status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo
        };

        db.DataProcessingActivities.Add(entity);
        audit.Add(httpContext, "privacy.processing_activity.created", nameof(DataProcessingActivity), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.Code, entity.Module, entity.ContainsSensitiveData, entity.HasInternationalTransfer });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/processing-activities/{entity.Id}", new { entity.Id });
    }

    private static async Task<IResult> GetDataSubjectRequestsAsync(
        string? status,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var query = db.DataSubjectRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var rows = await query
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.PersonId,
                x.RequestType,
                x.ReceivedAtUtc,
                x.Channel,
                x.IdentityVerified,
                x.Status,
                x.DueDate,
                x.ExtensionUntil,
                x.ResponsibleSubject,
                x.ClosedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateDataSubjectRequestAsync(
        CreateDataSubjectRequest request,
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

        if (!PrivacyCodes.DataSubjectRight.IsValid(request.RequestType))
        {
            return Results.BadRequest(new { message = "El tipo de derecho solicitado no es válido." });
        }

        if (request.PersonId is not null &&
            !await db.People.AnyAsync(x => x.Id == request.PersonId.Value, cancellationToken))
        {
            return Results.BadRequest(new { message = "La persona indicada no existe en la base maestra." });
        }

        var entity = new DataSubjectRequest
        {
            PersonId = request.PersonId,
            RequestType = request.RequestType,
            ReceivedAtUtc = request.ReceivedAtUtc ?? DateTimeOffset.UtcNow,
            Channel = request.Channel,
            IdentityVerified = request.IdentityVerified,
            Status = PrivacyCodes.Status.UnderReview,
            DueDate = request.DueDate,
            ResponsibleSubject = request.ResponsibleSubject,
            EvidenceReference = request.EvidenceReference
        };

        db.DataSubjectRequests.Add(entity);
        audit.Add(httpContext, "privacy.data_subject_request.created", nameof(DataSubjectRequest), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.RequestType, entity.PersonId, entity.IdentityVerified, entity.DueDate });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/data-subject-requests/{entity.Id}", new { entity.Id, entity.Status });
    }
}

public sealed record CreateProcessingActivityRequest(
    string Code,
    string Name,
    string Module,
    string Purpose,
    string LawfulBasis,
    string DataCategoriesJson,
    string SubjectCategoriesJson,
    bool ContainsSensitiveData,
    string? RecipientsJson,
    bool HasInternationalTransfer,
    Guid? RetentionPolicyId,
    string? InternalOwner,
    string Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record CreateDataSubjectRequest(
    Guid? PersonId,
    string RequestType,
    DateTimeOffset? ReceivedAtUtc,
    string Channel,
    bool IdentityVerified,
    DateOnly? DueDate,
    string? ResponsibleSubject,
    string? EvidenceReference);
