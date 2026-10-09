using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Ceremonies;

public static class AdvancementAttendanceEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
        => group.MapGet("/solicitudes/{requestId:guid}/avance/asistencias", GetAsync);

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
            .Select(x => new { x.Id, x.OrganizationId, x.CeremonyType, x.MemberId })
            .SingleOrDefaultAsync(cancellationToken);
        if (ceremony is null) return Results.NotFound();

        // No revelar evidencias de instrucción/tenida a un rol general o al
        // Tesorero; restringir a evaluación institucional o Secretaría del Taller.
        if (!access.CanEvaluateCeremonies(context.User) &&
            !access.CanManageLodgeSecretariat(context.User, ceremony.OrganizationId))
            return Results.Forbid();

        if (AdvancementAttendanceProjection.SourceGrade(ceremony.CeremonyType) is null)
            return Results.BadRequest(new { message = "La consulta sólo corresponde a Aumento de Salario o Exaltación." });
        if (ceremony.MemberId is null)
            return Results.Conflict(new { message = "No se ha asociado un hermano institucional a esta ceremonia." });

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var result = await AdvancementAttendanceProjection.GetAsync(
            ceremony.OrganizationId,
            ceremony.MemberId.Value,
            ceremony.CeremonyType,
            today,
            db,
            lodgeDb,
            cancellationToken);

        AdvancementSenioritySnapshot? seniority = null;
        if (result.Status == "ready" && result.Snapshot is not null)
        {
            seniority = await AdvancementSeniorityProjection.GetAsync(
                ceremony.MemberId.Value,
                result.Snapshot.GradeStartDate,
                today,
                db,
                cancellationToken);
        }

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            requestId,
            result.Status,
            result.Reason,
            result.Snapshot,
            seniority,
            minimumSeniorityRuleApplied = false,
            institutionalContinuityCertified = false,
            includesExcusesInPresence = false,
            workPapersEvaluated = false,
            authorizesCeremony = false
        });
    }
}
