using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyLegalRuleEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyLegalRuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy/legal-rules")
            .WithTags("Privacidad — Reglas jurídicas versionadas")
            .RequireAuthorization();

        group.MapGet("/deadlines", GetDeadlinesAsync);
        group.MapPost("/deadlines", CreateDeadlineVersionAsync);

        return endpoints;
    }

    private static async Task<IResult> GetDeadlinesAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var rows = await db.InstitutionalRuleSettings
            .AsNoTracking()
            .Where(x => x.Code.StartsWith(PrivacyLegalRuleCodes.Prefix) && x.Code.EndsWith(PrivacyLegalRuleCodes.Suffix))
            .OrderBy(x => x.Code)
            .ThenByDescending(x => x.EffectiveFrom)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Value,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.Status,
                x.SourceReference,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateDeadlineVersionAsync(
        CreatePrivacyDeadlineRuleRequest request,
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
            return Results.BadRequest(new { message = "El derecho de titular indicado no es válido." });
        }

        if (request.Days < 0 || request.Days > 3650)
        {
            return Results.BadRequest(new { message = "El plazo debe expresarse en días entre 0 y 3650." });
        }

        if (string.IsNullOrWhiteSpace(request.SourceReference))
        {
            return Results.BadRequest(new { message = "La referencia jurídica o institucional que sustenta el plazo es obligatoria." });
        }

        if (request.EffectiveTo is not null && request.EffectiveTo < request.EffectiveFrom)
        {
            return Results.BadRequest(new { message = "La fecha de término no puede ser anterior al inicio de vigencia." });
        }

        var code = PrivacyLegalRuleCodes.ResponseDays(request.RequestType);
        var duplicate = await db.InstitutionalRuleSettings.AnyAsync(x =>
            x.Code == code && x.EffectiveFrom == request.EffectiveFrom,
            cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Ya existe una versión del plazo con esa fecha de vigencia." });
        }

        var previous = await db.InstitutionalRuleSettings
            .Where(x =>
                x.Code == code &&
                x.Status == PrivacyCodes.Status.Active &&
                x.EffectiveFrom < request.EffectiveFrom &&
                (x.EffectiveTo == null || x.EffectiveTo >= request.EffectiveFrom))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is not null)
        {
            previous.EffectiveTo = request.EffectiveFrom.AddDays(-1);
            previous.Status = PrivacyCodes.Status.Retired;
        }

        var entity = new InstitutionalRuleSetting
        {
            Code = code,
            Value = request.Days.ToString(CultureInfo.InvariantCulture),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = PrivacyCodes.Status.Active,
            SourceReference = request.SourceReference.Trim()
        };

        db.InstitutionalRuleSettings.Add(entity);
        audit.Add(
            httpContext,
            "privacy.legal_deadline.version_created",
            nameof(InstitutionalRuleSetting),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                entity.Code,
                days = request.Days,
                entity.EffectiveFrom,
                entity.EffectiveTo,
                previousId = previous?.Id,
                sourceReference = entity.SourceReference
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/legal-rules/deadlines/{entity.Id}", new
        {
            entity.Id,
            entity.Code,
            days = request.Days,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.Status,
            previousVersionId = previous?.Id
        });
    }
}

public sealed record CreatePrivacyDeadlineRuleRequest(
    string RequestType,
    int Days,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceReference);
