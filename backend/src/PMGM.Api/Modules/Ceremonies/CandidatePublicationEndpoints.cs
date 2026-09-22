using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.CandidateIntake;

namespace PMGM.Api.Modules.Ceremonies;

public static class CandidatePublicationEndpoints
{
    public static IEndpointRouteBuilder MapCandidatePublicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/candidate-publications")
            .WithTags("Portal de Insinuados")
            .RequireAuthorization();

        group.MapGet("/active", GetActivePublicationsAsync);
        return endpoints;
    }

    private static async Task<IResult> GetActivePublicationsAsync(
        PmgmDbContext db,
        CandidateIntakeDbContext intakeDb,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var rows = await db.CandidatePublications
            .AsNoTracking()
            .Where(x =>
                x.Status == CeremonyCodes.PublicationStatus.Published &&
                x.PublishedFromUtc <= now &&
                (x.PublishedUntilUtc == null || x.PublishedUntilUtc >= now))
            .OrderBy(x => x.PublishedFromUtc)
            .Select(x => new
            {
                x.Id,
                x.CeremonyRequestId,
                x.Person.FirstNames,
                x.Person.LastNames,
                WorkshopName = x.Organization.Name,
                WorkshopNumber = x.Organization.Number,
                x.PublishedFromUtc,
                x.PublishedUntilUtc,
                x.RequiredDays,
                x.RuleCode,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var requestIds = rows.Select(x => x.CeremonyRequestId).ToArray();
        var requestIdsWithPhoto = await intakeDb.CandidateIntakeProfiles
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId) && x.PhotoVersionId != null)
            .Select(x => x.CeremonyRequestId)
            .ToListAsync(cancellationToken);
        var hasPhotoByRequestId = requestIdsWithPhoto.ToHashSet();

        var items = rows.Select(x => new CandidatePublicationPublicDto(
                DisplayName: $"{x.FirstNames} {x.LastNames}".Trim(),
                WorkshopName: x.WorkshopName,
                WorkshopNumber: x.WorkshopNumber,
                PublishedFromUtc: x.PublishedFromUtc,
                PublishedUntilUtc: x.PublishedUntilUtc,
                RequiredDays: x.RequiredDays,
                ElapsedDays: Math.Max(0, (int)Math.Floor((now - x.PublishedFromUtc).TotalDays)),
                ComplianceDateUtc: x.PublishedFromUtc.AddDays(x.RequiredDays),
                RuleCode: x.RuleCode,
                Status: x.Status,
                PhotoUrl: hasPhotoByRequestId.Contains(x.CeremonyRequestId)
                    ? $"/api/candidate-publications/{x.Id:D}/photo"
                    : null))
            .ToList();

        return Results.Ok(new
        {
            culture = "es-CL",
            portal = "Insinuados en período de publicación",
            total = items.Count,
            items
        });
    }
}
