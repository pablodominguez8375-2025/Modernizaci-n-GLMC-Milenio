using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyRetentionEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyRetentionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy/retention")
            .WithTags("Privacidad — Conservación y legal hold")
            .RequireAuthorization();

        group.MapGet("/holds", GetHoldsAsync);
        group.MapPost("/holds", CreateHoldAsync);
        group.MapPost("/holds/{id:guid}/release", ReleaseHoldAsync);
        group.MapGet("/evaluations", GetEvaluationsAsync);
        group.MapPost("/evaluate", EvaluateAsync);

        return endpoints;
    }

    private static async Task<IResult> GetHoldsAsync(
        string? entityType,
        string? entityId,
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

        var query = db.Set<DataRetentionHold>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(x => x.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(x => x.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var rows = await query
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.RetentionPolicyId,
                x.EntityType,
                x.EntityId,
                x.AuthoritySubject,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.Status,
                x.EvidenceReference,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateHoldAsync(
        CreateRetentionHoldRequest request,
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

        if (string.IsNullOrWhiteSpace(request.EntityType) ||
            string.IsNullOrWhiteSpace(request.EntityId) ||
            string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { message = "Tipo de entidad, identificador y motivo son obligatorios." });
        }

        if (request.EffectiveTo is not null && request.EffectiveTo < request.EffectiveFrom)
        {
            return Results.BadRequest(new { message = "La fecha de término del hold no puede ser anterior a su fecha de inicio." });
        }

        var policy = await db.DataRetentionPolicies
            .SingleOrDefaultAsync(x => x.Id == request.RetentionPolicyId, cancellationToken);

        if (policy is null)
        {
            return Results.NotFound(new { message = "La política de conservación indicada no existe." });
        }

        if (!policy.AllowsLegalHold)
        {
            return Results.BadRequest(new { message = "La política de conservación indicada no permite legal hold." });
        }

        var duplicate = await db.Set<DataRetentionHold>().AnyAsync(x =>
            x.RetentionPolicyId == request.RetentionPolicyId &&
            x.EntityType == request.EntityType &&
            x.EntityId == request.EntityId &&
            x.Status == PrivacyCodes.Status.Active,
            cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Ya existe un legal hold activo para esta entidad y política." });
        }

        var authoritySubject = ResolveSubject(httpContext.User);
        if (string.IsNullOrWhiteSpace(authoritySubject))
        {
            return Results.BadRequest(new { message = "No fue posible identificar al usuario que ordena el legal hold." });
        }

        var hold = new DataRetentionHold
        {
            RetentionPolicyId = request.RetentionPolicyId,
            EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId.Trim(),
            Reason = request.Reason.Trim(),
            AuthoritySubject = authoritySubject,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = PrivacyCodes.Status.Active,
            EvidenceReference = request.EvidenceReference
        };

        db.Set<DataRetentionHold>().Add(hold);
        audit.Add(
            httpContext,
            "privacy.retention_hold.created",
            nameof(DataRetentionHold),
            hold.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                hold.RetentionPolicyId,
                hold.EntityType,
                hold.EntityId,
                hold.EffectiveFrom,
                hold.EffectiveTo
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/privacy/retention/holds/{hold.Id}", new
        {
            hold.Id,
            hold.Status,
            hold.AuthoritySubject
        });
    }

    private static async Task<IResult> ReleaseHoldAsync(
        Guid id,
        ReleaseRetentionHoldRequest request,
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

        var hold = await db.Set<DataRetentionHold>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (hold is null)
        {
            return Results.NotFound(new { message = "El legal hold indicado no existe." });
        }

        if (hold.Status != PrivacyCodes.Status.Active)
        {
            return Results.Conflict(new { message = "El legal hold ya no se encuentra activo." });
        }

        var releaseDate = request.EffectiveTo ?? TodayInChile();
        if (releaseDate < hold.EffectiveFrom)
        {
            return Results.BadRequest(new { message = "La fecha de liberación no puede ser anterior al inicio del legal hold." });
        }

        hold.EffectiveTo = releaseDate;
        hold.Status = PrivacyCodes.Status.Closed;

        audit.Add(
            httpContext,
            "privacy.retention_hold.released",
            nameof(DataRetentionHold),
            hold.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                hold.RetentionPolicyId,
                hold.EntityType,
                hold.EntityId,
                hold.EffectiveTo,
                request.EvidenceReference
            });

        if (!string.IsNullOrWhiteSpace(request.EvidenceReference))
        {
            hold.EvidenceReference = request.EvidenceReference.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { hold.Id, hold.Status, hold.EffectiveTo });
    }

    private static async Task<IResult> GetEvaluationsAsync(
        string? entityType,
        string? entityId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var query = db.Set<DataRetentionEvaluation>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(x => x.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(x => x.EntityId == entityId);
        }

        var rows = await query
            .OrderByDescending(x => x.EvaluatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.RetentionPolicyId,
                x.EntityType,
                x.EntityId,
                x.AnchorDate,
                x.EvaluationDate,
                x.DueDate,
                x.RecommendedAction,
                x.BlockedByHold,
                x.RetentionHoldId,
                x.Rationale,
                x.Status,
                x.EvaluatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> EvaluateAsync(
        EvaluateRetentionRequest request,
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

        if (string.IsNullOrWhiteSpace(request.EntityType) || string.IsNullOrWhiteSpace(request.EntityId))
        {
            return Results.BadRequest(new { message = "Tipo e identificador de entidad son obligatorios." });
        }

        var policy = await db.DataRetentionPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.RetentionPolicyId, cancellationToken);

        if (policy is null)
        {
            return Results.NotFound(new { message = "La política de conservación indicada no existe." });
        }

        var evaluationDate = request.EvaluationDate ?? TodayInChile();
        var entityType = request.EntityType.Trim();
        var entityId = request.EntityId.Trim();

        var activeHold = await db.Set<DataRetentionHold>()
            .AsNoTracking()
            .Where(x =>
                x.RetentionPolicyId == request.RetentionPolicyId &&
                x.EntityType == entityType &&
                x.EntityId == entityId &&
                x.Status == PrivacyCodes.Status.Active &&
                x.EffectiveFrom <= evaluationDate &&
                (x.EffectiveTo == null || x.EffectiveTo >= evaluationDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        var decision = RetentionDecisionEngine.Evaluate(
            policy,
            request.AnchorDate,
            evaluationDate,
            activeHold is not null);

        var evaluation = new DataRetentionEvaluation
        {
            RetentionPolicyId = request.RetentionPolicyId,
            EntityType = entityType,
            EntityId = entityId,
            AnchorDate = request.AnchorDate,
            EvaluationDate = evaluationDate,
            DueDate = decision.DueDate,
            RecommendedAction = decision.RecommendedAction,
            BlockedByHold = decision.BlockedByHold,
            RetentionHoldId = activeHold?.Id,
            Rationale = decision.Rationale,
            Status = decision.BlockedByHold
                ? RetentionEvaluationStatuses.BlockedByHold
                : RetentionEvaluationStatuses.Ready
        };

        db.Set<DataRetentionEvaluation>().Add(evaluation);
        audit.Add(
            httpContext,
            "privacy.retention.evaluated",
            nameof(DataRetentionEvaluation),
            evaluation.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                evaluation.RetentionPolicyId,
                evaluation.EntityType,
                evaluation.EntityId,
                evaluation.AnchorDate,
                evaluation.EvaluationDate,
                evaluation.DueDate,
                evaluation.RecommendedAction,
                evaluation.BlockedByHold,
                evaluation.RetentionHoldId
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/privacy/retention/evaluations/{evaluation.Id}", new
        {
            evaluation.Id,
            evaluation.DueDate,
            evaluation.RecommendedAction,
            evaluation.BlockedByHold,
            evaluation.RetentionHoldId,
            evaluation.Status,
            evaluation.Rationale
        });
    }

    private static string? ResolveSubject(ClaimsPrincipal principal)
        => principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.Identity?.Name;

    private static DateOnly TodayInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record CreateRetentionHoldRequest(
    Guid RetentionPolicyId,
    string EntityType,
    string EntityId,
    string Reason,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? EvidenceReference);

public sealed record ReleaseRetentionHoldRequest(
    DateOnly? EffectiveTo,
    string? EvidenceReference);

public sealed record EvaluateRetentionRequest(
    Guid RetentionPolicyId,
    string EntityType,
    string EntityId,
    DateOnly AnchorDate,
    DateOnly? EvaluationDate);
