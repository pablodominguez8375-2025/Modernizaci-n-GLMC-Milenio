using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.InstitutionalProjections;

public static class CeremonyReviewQueueEndpoints
{
    public static IEndpointRouteBuilder MapCeremonyReviewQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/institutional/ceremonias/bandeja", GetQueueAsync)
            .WithTags("Proyecciones institucionales")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetQueueAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var user = httpContext.User;
        var orderReviewer = access.CanEvaluateCeremonies(user);
        var workshopReviewer = access.HasRole(user, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria);

        if (!orderReviewer && !workshopReviewer)
        {
            return Results.Forbid();
        }

        var scopedOrganizationIds = user.Claims
            .Where(x => x.Type == InstitutionalClaims.Organization)
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (!orderReviewer && scopedOrganizationIds.Length == 0)
        {
            return Results.Forbid();
        }

        var ceremonyQuery = db.CeremonyRequests.AsNoTracking();
        if (!orderReviewer)
        {
            ceremonyQuery = ceremonyQuery.Where(x => scopedOrganizationIds.Contains(x.OrganizationId));
        }

        var ceremonies = await ceremonyQuery
            .OrderBy(x => x.ProposedDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new CeremonyQueueRow(
                x.Id,
                x.OrganizationId,
                x.Organization.Name,
                x.Organization.Number,
                x.CeremonyType,
                x.ProposedDate,
                x.Status,
                x.CandidatePerson != null
                    ? (x.CandidatePerson.FirstNames + " " + x.CandidatePerson.LastNames).Trim()
                    : x.Member != null
                        ? (x.Member.Person.FirstNames + " " + x.Member.Person.LastNames).Trim()
                        : "Persona no asociada",
                x.CreatedAtUtc))
            .Take(500)
            .ToListAsync(cancellationToken);

        ceremonies = ceremonies
            .Where(x => access.CanReviewCeremonies(user, x.OrganizationId))
            .ToList();

        if (ceremonies.Count == 0)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new CeremonyReviewQueueResponse(0, []));
        }

        var requestIds = ceremonies.Select(x => x.Id).ToArray();
        var organizationIds = ceremonies.Select(x => x.OrganizationId).Distinct().ToArray();
        var today = ChileToday();
        var now = DateTimeOffset.UtcNow;

        var internalAffairsRows = await db.CeremonyValidations
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId) &&
                        x.ValidationType == CeremonyCodes.ValidationType.InternalAffairs)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.CeremonyRequestId, x.Status, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);

        var internalAffairsByRequest = internalAffairsRows
            .GroupBy(x => x.CeremonyRequestId)
            .ToDictionary(x => x.Key, x => x.First().Status);

        var grandMasterRows = await db.CeremonyValidations
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId) &&
                        x.ValidationType == CeremonyCodes.ValidationType.GrandMaster)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.CeremonyRequestId, x.Status, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);

        var grandMasterByRequest = grandMasterRows
            .GroupBy(x => x.CeremonyRequestId)
            .ToDictionary(x => x.Key, x => x.First().Status);

        var treasuryRows = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => organizationIds.Contains(x.OrganizationId) &&
                        x.MemberId == null &&
                        x.Scope == TreasuryCodes.RegularityScope.Organization &&
                        x.AsOfDate <= today)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.OrganizationId, x.Status, x.AsOfDate, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);

        var treasuryByOrganization = treasuryRows
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(x => x.Key, x => x.First().Status);

        var hospitalariaRows = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => organizationIds.Contains(x.OrganizationId) && x.AsOfDate <= today)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.OrganizationId, x.Status, x.AsOfDate, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);

        var hospitalariaByOrganization = hospitalariaRows
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(x => x.Key, x => x.First().Status);

        var publicationRows = await db.CandidatePublications
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId))
            .OrderByDescending(x => x.PublishedFromUtc)
            .Select(x => new CandidatePublicationRow(
                x.CeremonyRequestId,
                x.Status,
                x.RequiredDays,
                x.RuleCode,
                x.PublishedFromUtc,
                x.PublishedUntilUtc))
            .ToListAsync(cancellationToken);

        var publicationByRequest = publicationRows
            .GroupBy(x => x.CeremonyRequestId)
            .ToDictionary(x => x.Key, x => x.First());

        var items = ceremonies.Select(ceremony =>
        {
            internalAffairsByRequest.TryGetValue(ceremony.Id, out var regimenStatus);
            grandMasterByRequest.TryGetValue(ceremony.Id, out var grandMasterStatus);
            treasuryByOrganization.TryGetValue(ceremony.OrganizationId, out var treasuryStatus);
            hospitalariaByOrganization.TryGetValue(ceremony.OrganizationId, out var hospitalariaStatus);
            publicationByRequest.TryGetValue(ceremony.Id, out var publication);

            CandidatePublicationEvidence? publicationEvidence = null;
            CeremonyQueuePublicationDto? publicationProjection = null;
            if (ceremony.CeremonyType == CeremonyCodes.Type.Initiation && publication is not null)
            {
                var effectiveEnd = publication.PublishedUntilUtc is null || publication.PublishedUntilUtc > now
                    ? now
                    : publication.PublishedUntilUtc.Value;
                var completedDays = effectiveEnd < publication.PublishedFromUtc
                    ? 0
                    : Math.Max(0, (int)Math.Floor((effectiveEnd - publication.PublishedFromUtc).TotalDays));

                publicationEvidence = new CandidatePublicationEvidence(
                    Guid.Empty,
                    publication.Status,
                    publication.RequiredDays,
                    completedDays,
                    publication.RuleCode);

                publicationProjection = new CeremonyQueuePublicationDto(
                    publication.Status,
                    publication.RequiredDays,
                    completedDays,
                    publication.PublishedFromUtc,
                    publication.PublishedUntilUtc);
            }

            var decision = CeremonyEligibilityPolicy.Evaluate(
                ceremony.CeremonyType,
                regimenStatus,
                treasuryStatus,
                hospitalariaStatus,
                grandMasterStatus,
                publicationEvidence);

            var isFinal = ceremony.Status is CeremonyCodes.RequestStatus.Authorized or CeremonyCodes.RequestStatus.Rejected;
            var activePublication = publication is not null &&
                                    publication.Status == CeremonyCodes.PublicationStatus.Published &&
                                    publication.PublishedFromUtc <= now &&
                                    (publication.PublishedUntilUtc == null || publication.PublishedUntilUtc >= now);

            var actions = new CeremonyQueueActionsDto(
                CanValidateInternalAffairs: !isFinal && access.CanValidateCeremonyInternalAffairs(user),
                CanPublishCandidate: !isFinal &&
                                     ceremony.CeremonyType == CeremonyCodes.Type.Initiation &&
                                     !activePublication &&
                                     access.CanManageCandidatePublications(user, ceremony.OrganizationId),
                CanAuthorize: !isFinal && decision.CanAuthorize && access.CanAuthorizeCeremonies(user));

            var eligibility = new CeremonyQueueEligibilityDto(
                decision.Status,
                decision.CanAuthorize,
                decision.Requirements
                    .Select(x => new CeremonyQueueRequirementDto(x.Code, x.Name, x.Status, x.Reason))
                    .ToList(),
                publicationProjection);

            return new CeremonyReviewQueueItemDto(
                ceremony.Id,
                ceremony.OrganizationId,
                ceremony.OrganizationName,
                ceremony.OrganizationNumber,
                ceremony.CeremonyType,
                ceremony.SubjectDisplayName,
                ceremony.ProposedDate,
                ceremony.Status,
                eligibility,
                actions,
                ceremony.CreatedAtUtc);
        }).ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new CeremonyReviewQueueResponse(items.Count, items));
    }

    private static DateOnly ChileToday()
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }

    private sealed record CeremonyQueueRow(
        Guid Id,
        Guid OrganizationId,
        string OrganizationName,
        string? OrganizationNumber,
        string CeremonyType,
        DateOnly? ProposedDate,
        string Status,
        string SubjectDisplayName,
        DateTimeOffset CreatedAtUtc);

    private sealed record CandidatePublicationRow(
        Guid CeremonyRequestId,
        string Status,
        int RequiredDays,
        string RuleCode,
        DateTimeOffset PublishedFromUtc,
        DateTimeOffset? PublishedUntilUtc);
}

public sealed record CeremonyReviewQueueItemDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string? OrganizationNumber,
    string CeremonyType,
    string SubjectDisplayName,
    DateOnly? ProposedDate,
    string Status,
    CeremonyQueueEligibilityDto Eligibility,
    CeremonyQueueActionsDto Actions,
    DateTimeOffset CreatedAtUtc);

public sealed record CeremonyQueueEligibilityDto(
    string Status,
    bool CanAuthorize,
    IReadOnlyList<CeremonyQueueRequirementDto> Requirements,
    CeremonyQueuePublicationDto? Publication);

public sealed record CeremonyQueueRequirementDto(
    string Code,
    string Name,
    string Status,
    string Reason);

public sealed record CeremonyQueuePublicationDto(
    string Status,
    int RequiredDays,
    int CompletedDays,
    DateTimeOffset PublishedFromUtc,
    DateTimeOffset? PublishedUntilUtc);

public sealed record CeremonyQueueActionsDto(
    bool CanValidateInternalAffairs,
    bool CanPublishCandidate,
    bool CanAuthorize);

public sealed record CeremonyReviewQueueResponse(
    int Total,
    IReadOnlyList<CeremonyReviewQueueItemDto> Items);
