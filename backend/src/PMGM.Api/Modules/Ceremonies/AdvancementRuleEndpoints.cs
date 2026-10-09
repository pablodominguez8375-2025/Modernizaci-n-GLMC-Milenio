using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public static class AdvancementRuleEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/reglas/avance", SetAsync);
        group.MapGet("/reglas/avance", GetAsync);
    }

    private static async Task<IResult> SetAsync(
        AdvancementRuleRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        // No atribuye competencias nuevas a cargos de Taller ni a Gran Secretaría.
        if (!access.HasOrderScope(httpContext.User) ||
            !access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin))
            return Results.Forbid();

        var code = AdvancementRulePolicy.CodeFor(request.CeremonyType);
        if (code is null)
            return Results.BadRequest(new { message = "Esta configuración sólo corresponde a Aumento de Salario o Exaltación." });

        var configuration = new AdvancementThresholdConfiguration(
            request.MinimumMeetingAttendance,
            request.MinimumInstructionAttendance,
            request.MinimumWorkPapers);
        var reference = request.SourceReference?.Trim();
        if (!AdvancementRulePolicy.IsValid(configuration) ||
            string.IsNullOrWhiteSpace(reference) || reference.Length > 500)
            return Results.BadRequest(new { message = "Los tres mínimos deben ser no negativos y la resolución/decreto es obligatoria (máximo 500 caracteres)." });

        var today = ChileToday();
        if (request.EffectiveFrom < today)
            return Results.BadRequest(new { message = "Las reglas nuevas se configuran hacia adelante; las vigencias históricas no pueden modificarse." });

        var existing = await db.InstitutionalRuleSettings
            .Where(x => x.Code == code && x.Status == "active")
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);
        if (existing.Any(x => x.EffectiveFrom >= request.EffectiveFrom))
            return Results.Conflict(new { message = "Ya existe una versión desde esa fecha o una versión posterior; las vigencias deben avanzar en orden cronológico." });

        var previousOpen = existing.Where(x => x.EffectiveTo == null).ToArray();
        if (previousOpen.Length > 1 ||
            existing.Any(x => x.EffectiveTo >= request.EffectiveFrom))
            return Results.Conflict(new { message = "El histórico contiene vigencias incompatibles; revisar antes de guardar una nueva regla." });

        if (previousOpen.Length == 1)
            previousOpen[0].EffectiveTo = request.EffectiveFrom.AddDays(-1);

        var rule = new InstitutionalRuleSetting
        {
            Code = code,
            Value = AdvancementRulePolicy.Serialize(configuration),
            EffectiveFrom = request.EffectiveFrom,
            Status = "active",
            SourceReference = reference
        };
        db.InstitutionalRuleSettings.Add(rule);
        audit.Add(
            httpContext,
            "ceremony.rule.advancement.versioned",
            nameof(InstitutionalRuleSetting),
            rule.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                rule.Code,
                rule.EffectiveFrom,
                rule.SourceReference,
                configuration.MinimumMeetingAttendance,
                configuration.MinimumInstructionAttendance,
                configuration.MinimumWorkPapers,
                previousRuleId = previousOpen.Length == 1 ? (Guid?)previousOpen[0].Id : null
            });

        await db.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created("/api/ceremonias/reglas/avance", ToResponse(
            new AdvancementRuleSnapshot(
                rule.Id,
                code,
                rule.EffectiveFrom,
                rule.EffectiveTo,
                reference,
                configuration,
                new AdvancementThresholds(
                    configuration.MinimumMeetingAttendance,
                    configuration.MinimumInstructionAttendance,
                    configuration.MinimumWorkPapers,
                    rule.Id.ToString("N"))), today));
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

        var code = AdvancementRulePolicy.CodeFor(ceremonyType);
        if (code is null)
            return Results.BadRequest(new { message = "Se debe consultar Aumento de Salario o Exaltación." });

        var cutoff = asOf ?? ChileToday();
        var candidates = await db.InstitutionalRuleSettings
            .AsNoTracking()
            .Where(x => x.Code == code && x.Status == "active" && x.EffectiveFrom <= cutoff &&
                        (x.EffectiveTo == null || x.EffectiveTo >= cutoff))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return Results.NotFound(new { message = "No existe una regla institucional vigente; el avance no puede evaluarse como cumplido." });

        var rule = AdvancementRulePolicy.Resolve(ceremonyType, cutoff, candidates);
        if (rule is null)
            return Results.Conflict(new { message = "La regla vigente está incompleta, no tiene resolución respaldatoria o registra vigencias solapadas." });

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(ToResponse(rule, cutoff));
    }

    private static object ToResponse(AdvancementRuleSnapshot rule, DateOnly asOf)
        => new
        {
            ruleId = rule.RuleId,
            ruleCode = rule.RuleCode,
            version = rule.Thresholds.RuleVersion,
            asOf,
            rule.EffectiveFrom,
            rule.EffectiveTo,
            rule.SourceReference,
            rule.Configuration.MinimumMeetingAttendance,
            rule.Configuration.MinimumInstructionAttendance,
            rule.Configuration.MinimumWorkPapers
        };

    private static DateOnly ChileToday()
        => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
}

public sealed record AdvancementRuleRequest(
    string CeremonyType,
    DateOnly EffectiveFrom,
    int MinimumMeetingAttendance,
    int MinimumInstructionAttendance,
    int MinimumWorkPapers,
    string? SourceReference);
