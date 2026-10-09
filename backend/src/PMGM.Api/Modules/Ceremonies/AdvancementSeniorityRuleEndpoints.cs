using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Regla de antigüedad versionada y consultable, separada de los tres contadores
/// históricos. Ninguna operación de este endpoint certifica membresía.
/// </summary>
public static class AdvancementSeniorityRuleEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/reglas/avance/antiguedad", SetAsync);
        group.MapGet("/reglas/avance/antiguedad", GetAsync);
    }

    private static async Task<IResult> SetAsync(
        AdvancementSeniorityRuleRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.HasOrderScope(httpContext.User) ||
            !access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin))
            return Results.Forbid();

        var code = AdvancementSeniorityRulePolicy.CodeFor(request.CeremonyType);
        if (code is null)
            return Results.BadRequest(new { message = "La regla de antigüedad sólo admite Aumento o Exaltación." });

        var reference = request.SourceReference?.Trim();
        if (!AdvancementSeniorityRulePolicy.IsValidMinimum(request.MinimumCompleteMonths) ||
            string.IsNullOrWhiteSpace(reference) || reference.Length > 500)
            return Results.BadRequest(new { message = "Indique un mínimo positivo de meses completos y resolución/decreto (máximo 500 caracteres)." });

        var today = ChileToday();
        if (request.EffectiveFrom < today)
            return Results.BadRequest(new { message = "Las reglas nuevas rigen hacia adelante, sin reescribir vigencias anteriores." });

        var active = await db.InstitutionalRuleSettings
            .Where(x => x.Code == code && x.Status == "active")
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);
        if (active.Any(x => x.EffectiveFrom >= request.EffectiveFrom))
            return Results.Conflict(new { message = "Existe una regla desde esa fecha o posterior; las vigencias deben avanzar." });

        var open = active.Where(x => x.EffectiveTo is null).ToArray();
        if (open.Length > 1 || active.Any(x => x.EffectiveTo >= request.EffectiveFrom))
            return Results.Conflict(new { message = "Existen vigencias superpuestas o inconsistentes; revisar el histórico." });

        if (open.Length == 1)
            open[0].EffectiveTo = request.EffectiveFrom.AddDays(-1);

        var rule = new InstitutionalRuleSetting
        {
            Code = code,
            Value = AdvancementSeniorityRulePolicy.Serialize(request.MinimumCompleteMonths),
            EffectiveFrom = request.EffectiveFrom,
            Status = "active",
            SourceReference = reference
        };
        db.InstitutionalRuleSettings.Add(rule);
        audit.Add(httpContext, "ceremony.rule.advancement.seniority.versioned",
            nameof(InstitutionalRuleSetting), rule.Id.ToString(),
            null, AuditResults.Success,
            new
            {
                rule.Code,
                rule.EffectiveFrom,
                rule.SourceReference,
                request.MinimumCompleteMonths,
                previousRuleId = open.Length == 1 ? (Guid?)open[0].Id : null
            });
        await db.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created("/api/ceremonias/reglas/avance/antiguedad",
            ToResponse(new AdvancementSeniorityRuleSnapshot(
                rule.Id, rule.Code, rule.EffectiveFrom, rule.EffectiveTo,
                reference, request.MinimumCompleteMonths), today));
    }

    private static async Task<IResult> GetAsync(
        string ceremonyType,
        DateOnly? asOf,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanEvaluateCeremonies(httpContext.User) &&
            !access.HasOrderScope(httpContext.User))
            return Results.Forbid();

        var code = AdvancementSeniorityRulePolicy.CodeFor(ceremonyType);
        if (code is null)
            return Results.BadRequest(new { message = "Se debe indicar Aumento o Exaltación." });

        var date = asOf ?? ChileToday();
        var candidates = await db.InstitutionalRuleSettings.AsNoTracking()
            .Where(x => x.Code == code && x.Status == "active" &&
                        x.EffectiveFrom <= date &&
                        (x.EffectiveTo == null || x.EffectiveTo >= date))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return Results.NotFound(new { message = "No existe regla institucional de antigüedad vigente." });

        var snapshot = AdvancementSeniorityRulePolicy.Resolve(ceremonyType, date, candidates);
        if (snapshot is null)
            return Results.Conflict(new { message = "La regla de antigüedad está incompleta o tiene vigencias incompatibles." });

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(ToResponse(snapshot, date));
    }

    private static object ToResponse(AdvancementSeniorityRuleSnapshot rule, DateOnly asOf)
        => new
        {
            ruleId = rule.RuleId,
            ruleCode = rule.RuleCode,
            version = rule.RuleId.ToString("N"),
            asOf,
            rule.EffectiveFrom,
            rule.EffectiveTo,
            rule.SourceReference,
            rule.MinimumCompleteMonths,
            institutionalContinuityCertified = false,
            authorizesCeremony = false
        };

    private static DateOnly ChileToday() => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
}

public sealed record AdvancementSeniorityRuleRequest(
    string CeremonyType,
    DateOnly EffectiveFrom,
    int MinimumCompleteMonths,
    string? SourceReference);
