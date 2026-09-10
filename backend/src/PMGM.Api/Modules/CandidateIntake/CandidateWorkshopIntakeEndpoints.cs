using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateWorkshopIntakeEndpoints
{
    public static IEndpointRouteBuilder MapCandidateWorkshopIntakeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/insinuados/taller/solicitudes", GetWorkshopQueueAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetWorkshopQueueAsync(
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeOperations(httpContext.User) && !access.CanManageGrandSecretariat(httpContext.User))
            return Results.Forbid();

        var candidates = await coreDb.CeremonyRequests
            .AsNoTracking()
            .Where(x => x.CeremonyType == CeremonyCodes.Type.Initiation && x.CandidatePersonId != null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                WorkshopName = x.Organization.Name,
                WorkshopNumber = x.Organization.Number,
                FirstNames = x.CandidatePerson != null ? x.CandidatePerson.FirstNames : "",
                LastNames = x.CandidatePerson != null ? x.CandidatePerson.LastNames : "",
                x.ProposedDate,
                x.Status,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var allowed = candidates
            .Where(x => access.CanManageOrganization(httpContext.User, x.OrganizationId) || access.CanManageGrandSecretariat(httpContext.User))
            .ToList();

        var requestIds = allowed.Select(x => x.Id).ToArray();
        if (requestIds.Length == 0)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { total = 0, items = Array.Empty<CandidateWorkshopQueueItemDto>() });
        }

        var profiles = await intakeDb.CandidateIntakeProfiles
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId))
            .Select(x => new { x.CeremonyRequestId, x.PhotoVersionId })
            .ToDictionaryAsync(x => x.CeremonyRequestId, cancellationToken);

        var reviewRows = await coreDb.CeremonyValidations
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId) && x.ValidationType == CeremonyCodes.ValidationType.CandidatePublicationReview)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.CeremonyRequestId, x.Status, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);
        var latestReviews = reviewRows
            .GroupBy(x => x.CeremonyRequestId)
            .ToDictionary(group => group.Key, group => group.First().Status);

        var published = (await coreDb.CandidatePublications
                .AsNoTracking()
                .Where(x => requestIds.Contains(x.CeremonyRequestId) && x.Status == CeremonyCodes.PublicationStatus.Published)
                .Select(x => x.CeremonyRequestId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var items = allowed.Select(candidate =>
        {
            profiles.TryGetValue(candidate.Id, out var profile);
            var reviewStatus = published.Contains(candidate.Id)
                ? CandidateIntakeCodes.ReviewStatus.Approved
                : latestReviews.TryGetValue(candidate.Id, out var review)
                    ? MapReviewStatus(review)
                    : CandidateIntakeCodes.ReviewStatus.Pending;

            return new CandidateWorkshopQueueItemDto(
                candidate.Id,
                candidate.FirstNames,
                candidate.LastNames,
                $"{candidate.FirstNames} {candidate.LastNames}".Trim(),
                candidate.WorkshopName,
                candidate.WorkshopNumber,
                candidate.ProposedDate,
                candidate.Status,
                profile is not null,
                profile?.PhotoVersionId is not null,
                reviewStatus,
                candidate.CreatedAtUtc);
        }).ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static string MapReviewStatus(string? status)
        => status switch
        {
            CeremonyCodes.ValidationStatus.Approved => CandidateIntakeCodes.ReviewStatus.Approved,
            CeremonyCodes.ValidationStatus.Observed => CandidateIntakeCodes.ReviewStatus.Observed,
            CeremonyCodes.ValidationStatus.Rejected => CandidateIntakeCodes.ReviewStatus.Rejected,
            _ => CandidateIntakeCodes.ReviewStatus.Pending
        };
}

public sealed record CandidateWorkshopQueueItemDto(
    Guid CeremonyRequestId,
    string FirstNames,
    string LastNames,
    string DisplayName,
    string WorkshopName,
    string? WorkshopNumber,
    DateOnly? ProposedDate,
    string RequestStatus,
    bool ProfileAvailable,
    bool PhotoAvailable,
    string ReviewStatus,
    DateTimeOffset CreatedAtUtc);
