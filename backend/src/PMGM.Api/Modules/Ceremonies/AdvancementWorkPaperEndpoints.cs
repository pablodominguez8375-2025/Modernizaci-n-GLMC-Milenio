using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.LodgeManagement;

namespace PMGM.Api.Modules.Ceremonies;

public static class AdvancementWorkPaperEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
        => group.MapGet("/solicitudes/{requestId:guid}/avance/planchas", GetAsync);

    private static async Task<IResult> GetAsync(
        Guid requestId,
        HttpContext context,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        DocumentManagementDbContext documentsDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .Where(x => x.Id == requestId)
            .Select(x => new { x.OrganizationId, x.MemberId, x.CeremonyType })
            .SingleOrDefaultAsync(cancellationToken);
        if (ceremony is null) return Results.NotFound();

        if (!access.CanEvaluateCeremonies(context.User) &&
            !access.CanManageLodgeSecretariat(context.User, ceremony.OrganizationId))
            return Results.Forbid();

        var sourceGrade = AdvancementAttendanceProjection.SourceGrade(ceremony.CeremonyType);
        if (sourceGrade is null)
            return Results.BadRequest(new { message = "Esta consulta sólo corresponde a Aumento de Salario o Exaltación." });
        if (ceremony.MemberId is null)
            return Results.Conflict(new { message = "La solicitud no identifica al hermano del Taller." });

        var sourceDegree = LodgeManagementCodes.Grade.ToNumeric(sourceGrade);
        if (sourceDegree is null)
            return Results.Conflict(new { message = "El grado de procedencia no está reconocido." });

        var cutoff = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var attendance = await AdvancementAttendanceProjection.GetAsync(
            ceremony.OrganizationId, ceremony.MemberId.Value,
            ceremony.CeremonyType, cutoff, db, lodgeDb, cancellationToken);

        context.Response.Headers.CacheControl = "private, no-store";
        if (attendance.Status != "ready" || attendance.Snapshot is null)
            return Results.Ok(new
            {
                requestId,
                attendance.Status,
                attendance.Reason,
                workPapers = AdvancementWorkPaperReviewPolicy.Summarize(Array.Empty<WorkPaperReviewCandidate>()),
                authorizesCeremony = false
            });

        var docs = await documentsDb.InstitutionalDocuments.AsNoTracking()
            .Include(x => x.Versions)
            .Where(x => x.DocumentType == "work_paper" &&
                        x.AuthorMemberId == ceremony.MemberId.Value &&
                        x.OrganizationId == ceremony.OrganizationId &&
                        x.MinimumDegreeRequired == sourceDegree.Value &&
                        x.Status != DocumentManagementCodes.DocumentStatus.Retired)
            .ToListAsync(cancellationToken);

        var from = attendance.Snapshot.GradeStartDate;
        var candidates = docs.Select(document =>
        {
            var versions = document.Versions.Select(version =>
                new WorkPaperReviewVersion(
                    version.Id,
                    version.VersionNumber,
                    DateOnly.FromDateTime(
                        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(version.CreatedAtUtc, "America/Santiago").DateTime),
                    version.AuthorEffectiveDegreeAtUpload,
                    version.ProcessingStatus,
                    version.Sha256,
                    version.ScanReference,
                    document.PublishedVersionId == version.Id));

            return AdvancementWorkPaperReviewPolicy.Assess(
                document.Id, document.Title,
                document.Status == DocumentManagementCodes.DocumentStatus.Published,
                from, cutoff, sourceDegree.Value, versions);
        }).ToArray();

        return Results.Ok(new
        {
            requestId,
            gradeStartDate = from,
            asOf = cutoff,
            sourceGrade,
            workPapers = AdvancementWorkPaperReviewPolicy.Summarize(candidates),
            authorizesCeremony = false
        });
    }
}
