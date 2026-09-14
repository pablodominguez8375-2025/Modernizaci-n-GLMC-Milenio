using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.RegimenInterior;

public static class RegimenInteriorEndpoints
{
    public static IEndpointRouteBuilder MapRegimenInteriorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/regimen-interior")
            .WithTags("Régimen Interior")
            .RequireAuthorization();

        group.MapGet("/summary", GetSummaryAsync);

        return endpoints;
    }

    private static async Task<IResult> GetSummaryAsync(
        Guid? organizationId,
        DateOnly? asOf,
        DateOnly? from,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User))
        {
            return Results.Forbid();
        }

        if (organizationId is not null)
        {
            var organizationExists = await db.Organizations
                .AsNoTracking()
                .AnyAsync(x => x.Id == organizationId.Value, cancellationToken);

            if (!organizationExists)
            {
                return Results.NotFound(new { message = "El Taller u organización indicada no existe." });
            }
        }

        var cutoff = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = from ?? new DateOnly(cutoff.Year, 1, 1);

        if (periodStart > cutoff)
        {
            return Results.BadRequest(new { message = "La fecha inicial del período no puede ser posterior a la fecha de corte." });
        }

        var historicalMemberships = db.Memberships
            .AsNoTracking()
            .Where(x => x.StartDate <= cutoff);

        if (organizationId is not null)
        {
            historicalMemberships = historicalMemberships.Where(x => x.OrganizationId == organizationId.Value);
        }

        var relatedMemberIds = await historicalMemberships
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var currentMemberships = historicalMemberships
            .Where(x => x.EndDate == null || x.EndDate >= cutoff);

        var currentAffiliations = await currentMemberships
            .Select(x => new AffiliationRow(x.MemberId, x.OrganizationId))
            .Distinct()
            .ToListAsync(cancellationToken);

        var currentMemberIds = currentAffiliations
            .Select(x => x.MemberId)
            .Distinct()
            .ToList();

        var knownStatusCodes = new[]
        {
            MembershipCodes.InstitutionalStatus.Active,
            MembershipCodes.InstitutionalStatus.Inactive,
            MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal,
            MembershipCodes.InstitutionalStatus.ForcedWithdrawal,
            MembershipCodes.InstitutionalStatus.Reinstated,
            MembershipCodes.InstitutionalStatus.Deceased
        };

        var statusRows = relatedMemberIds.Count == 0
            ? []
            : await db.InstitutionalStatusEvents
                .AsNoTracking()
                .Where(x => relatedMemberIds.Contains(x.MemberId) &&
                            x.EffectiveDate <= cutoff &&
                            knownStatusCodes.Contains(x.EventType))
                .Select(x => new StatusRow(x.MemberId, x.EventType, x.EffectiveDate, x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var latestStatusByMember = statusRows
            .GroupBy(x => x.MemberId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.EffectiveDate)
                    .ThenByDescending(y => y.RecordedAtUtc)
                    .First().Status);

        var currentActive = currentMemberIds.Count(memberId =>
            !latestStatusByMember.TryGetValue(memberId, out var status) ||
            status is MembershipCodes.InstitutionalStatus.Active or MembershipCodes.InstitutionalStatus.Reinstated);

        var currentInactive = currentMemberIds.Count(memberId =>
            latestStatusByMember.TryGetValue(memberId, out var status) &&
            status == MembershipCodes.InstitutionalStatus.Inactive);

        var currentWithBlockingStatus = currentMemberIds.Count(memberId =>
            latestStatusByMember.TryGetValue(memberId, out var status) &&
            status is MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal
                or MembershipCodes.InstitutionalStatus.ForcedWithdrawal
                or MembershipCodes.InstitutionalStatus.Deceased);

        var historicalEventsQuery = db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => relatedMemberIds.Contains(x.MemberId) &&
                        x.EffectiveDate >= periodStart &&
                        x.EffectiveDate <= cutoff);

        if (organizationId is not null)
        {
            historicalEventsQuery = historicalEventsQuery.Where(x =>
                x.OrganizationId == organizationId.Value || x.OrganizationId == null);
        }

        var historicalEvents = relatedMemberIds.Count == 0
            ? []
            : await historicalEventsQuery
                .Select(x => x.EventType)
                .ToListAsync(cancellationToken);

        var degreeRows = currentMemberIds.Count == 0
            ? []
            : await db.DegreeEvents
                .AsNoTracking()
                .Where(x => currentMemberIds.Contains(x.MemberId) && x.EffectiveDate <= cutoff)
                .Select(x => new DegreeRow(x.MemberId, x.Degree, x.EffectiveDate, x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var degreeDistribution = degreeRows
            .GroupBy(x => x.MemberId)
            .Select(x => x.OrderByDescending(y => y.EffectiveDate)
                .ThenByDescending(y => y.RecordedAtUtc)
                .First().Degree)
            .GroupBy(x => x)
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Count());

        var financialRows = currentMemberIds.Count == 0
            ? []
            : await db.FinancialRegularitySnapshots
                .AsNoTracking()
                .Where(x => x.MemberId != null &&
                            currentMemberIds.Contains(x.MemberId.Value) &&
                            x.AsOfDate <= cutoff &&
                            (organizationId == null || x.OrganizationId == organizationId.Value))
                .Select(x => new FinancialRow(
                    x.MemberId!.Value,
                    x.OrganizationId,
                    x.Status,
                    x.AsOfDate,
                    x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var latestFinancialByAffiliation = financialRows
            .GroupBy(x => new { x.MemberId, x.OrganizationId })
            .Select(x => x
                .OrderByDescending(y => y.AsOfDate)
                .ThenByDescending(y => y.RecordedAtUtc)
                .First())
            .ToList();

        var financialKeys = latestFinancialByAffiliation
            .Select(x => (x.MemberId, x.OrganizationId))
            .ToHashSet();

        var affiliationsWithoutFinancialStatus = currentAffiliations.Count(x =>
            !financialKeys.Contains((x.MemberId, x.OrganizationId)));

        var delinquentMemberIds = latestFinancialByAffiliation
            .Where(x => x.Status == TreasuryCodes.RegularityStatus.Delinquent)
            .Select(x => x.MemberId)
            .Distinct()
            .ToHashSet();

        var pendingTransfersQuery = db.MemberTransfers
            .AsNoTracking()
            .Where(x => x.Status == MembershipCodes.TransferStatus.Requested ||
                        x.Status == MembershipCodes.TransferStatus.Approved);

        if (organizationId is not null)
        {
            pendingTransfersQuery = pendingTransfersQuery.Where(x =>
                x.SourceOrganizationId == organizationId.Value ||
                x.TargetOrganizationId == organizationId.Value);
        }

        var pendingTransfers = await pendingTransfersQuery.CountAsync(cancellationToken);

        return Results.Ok(new
        {
            scope = organizationId is null ? "order" : "organization",
            organizationId,
            asOf = cutoff,
            period = new { from = periodStart, to = cutoff },
            members = new
            {
                totalRelated = relatedMemberIds.Count,
                currentlyAffiliated = currentMemberIds.Count,
                active = currentActive,
                inactive = currentInactive,
                currentWithBlockingStatus
            },
            events = new
            {
                voluntaryWithdrawals = historicalEvents.Count(x => x == MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal),
                forcedWithdrawals = historicalEvents.Count(x => x == MembershipCodes.InstitutionalStatus.ForcedWithdrawal),
                reinstatements = historicalEvents.Count(x => x == MembershipCodes.InstitutionalStatus.Reinstated),
                deaths = historicalEvents.Count(x => x == MembershipCodes.InstitutionalStatus.Deceased),
                transfers = historicalEvents.Count(x => x == MembershipCodes.InstitutionalStatus.WorkshopTransfer)
            },
            financialRegularity = new
            {
                source = "Gran Tesorería",
                currentAffiliations = currentAffiliations.Count,
                upToDate = latestFinancialByAffiliation.Count(x => x.Status == TreasuryCodes.RegularityStatus.UpToDate),
                delinquent = latestFinancialByAffiliation.Count(x => x.Status == TreasuryCodes.RegularityStatus.Delinquent),
                pending = latestFinancialByAffiliation.Count(x => x.Status == TreasuryCodes.RegularityStatus.Pending),
                exempt = latestFinancialByAffiliation.Count(x => x.Status == TreasuryCodes.RegularityStatus.Exempt),
                withoutStatus = affiliationsWithoutFinancialStatus,
                delinquentMembersDistinct = delinquentMemberIds.Count
            },
            degreeDistribution,
            pendingTransfers
        });
    }

    private sealed record AffiliationRow(Guid MemberId, Guid OrganizationId);

    private sealed record StatusRow(
        Guid MemberId,
        string Status,
        DateOnly EffectiveDate,
        DateTimeOffset RecordedAtUtc);

    private sealed record DegreeRow(
        Guid MemberId,
        string Degree,
        DateOnly EffectiveDate,
        DateTimeOffset RecordedAtUtc);

    private sealed record FinancialRow(
        Guid MemberId,
        Guid OrganizationId,
        string Status,
        DateOnly AsOfDate,
        DateTimeOffset RecordedAtUtc);
}
