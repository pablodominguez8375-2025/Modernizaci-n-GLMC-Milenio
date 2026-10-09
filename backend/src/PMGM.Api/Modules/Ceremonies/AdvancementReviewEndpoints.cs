using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Unifica las fuentes existentes de evidencia, sin permitir la autorización
/// por inferencia de conteos ni por documentos no acreditados.
/// </summary>
public static class AdvancementReviewEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
        => group.MapGet("/solicitudes/{requestId:guid}/avance/resumen", GetAsync);

    private static async Task<IResult> GetAsync(
        Guid requestId,
        HttpContext context,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .Where(x => x.Id == requestId)
            .Select(x => new { x.Id, x.OrganizationId, x.MemberId, x.CeremonyType })
            .SingleOrDefaultAsync(cancellationToken);
        if (ceremony is null) return Results.NotFound();

        if (!access.CanEvaluateCeremonies(context.User) &&
            !access.CanManageLodgeSecretariat(context.User, ceremony.OrganizationId))
            return Results.Forbid();

        if (AdvancementAttendanceProjection.SourceGrade(ceremony.CeremonyType) is null)
            return Results.BadRequest(new { message = "Sólo corresponde a Aumento de Salario o Exaltación." });
        if (ceremony.MemberId is null)
            return Results.Conflict(new { message = "No se ha identificado al hermano de la solicitud." });

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow, "America/Santiago").DateTime);

        var countCode = AdvancementRulePolicy.CodeFor(ceremony.CeremonyType)!;
        var seniorityCode = AdvancementSeniorityRulePolicy.CodeFor(ceremony.CeremonyType)!;
        var rules = await db.InstitutionalRuleSettings.AsNoTracking()
            .Where(x => (x.Code == countCode || x.Code == seniorityCode) &&
                        x.Status == "active" && x.EffectiveFrom <= today &&
                        (x.EffectiveTo == null || x.EffectiveTo >= today))
            .ToListAsync(cancellationToken);

        var thresholds = AdvancementRulePolicy.Resolve(ceremony.CeremonyType, today, rules);
        var seniorityRule = AdvancementSeniorityRulePolicy.Resolve(ceremony.CeremonyType, today, rules);
        var attendance = await AdvancementAttendanceProjection.GetAsync(
            ceremony.OrganizationId, ceremony.MemberId.Value,
            ceremony.CeremonyType, today, db, lodgeDb, cancellationToken);

        AdvancementSenioritySnapshot? chronology = null;
        if (attendance.Status == "ready" && attendance.Snapshot is not null)
        {
            chronology = await AdvancementSeniorityProjection.GetAsync(
                ceremony.MemberId.Value, attendance.Snapshot.GradeStartDate,
                today, db, cancellationToken);
        }

        var seniority = AdvancementSeniorityRulePolicy.Review(seniorityRule, chronology);
        var matrix = AdvancementReviewMatrixPolicy.Build(
            ceremony.CeremonyType, attendance, thresholds, seniority);

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            requestId,
            memberId = ceremony.MemberId.Value,
            organizationId = ceremony.OrganizationId,
            evaluatedAt = today,
            matrix,
            attendanceStatus = attendance.Status,
            attendanceReason = attendance.Reason,
            seniorityStatus = seniority.Status,
            minimumSeniorityRuleApplied = false,
            presentationEvidenceCertified = false,
            institutionalContinuityCertified = false,
            // Se consulta aparte el detalle de versiones y enlaces de planchas.
            workPaperReviewPath = $"/api/ceremonias/solicitudes/{requestId}/avance/planchas",
            authorizesCeremony = false
        });
    }
}
