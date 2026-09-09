using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Core;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/institutional/organizations/options", GetOrganizationOptionsAsync)
            .WithTags("Organizaciones institucionales")
            .RequireAuthorization();

        endpoints.MapGet("/api/institutional/organizations/{id:guid}/profile", GetOrganizationProfileAsync)
            .WithTags("Organizaciones institucionales")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetOrganizationOptionsAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var rows = await db.Organizations
            .AsNoTracking()
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Number)
            .ThenBy(x => x.Name)
            .Select(x => new OrganizationOptionDto(x.Id, x.Name, x.Number, x.Type))
            .Take(2000)
            .ToListAsync(cancellationToken);

        var items = rows
            .Where(x => access.CanReadOrganization(httpContext.User, x.Id))
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new OrganizationOptionsResponse(items.Count, items));
    }

    private static async Task<IResult> GetOrganizationProfileAsync(
        Guid id,
        HttpContext httpContext,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Number,
                x.Type,
                x.ParentOrganizationId,
                x.CreatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return Results.NotFound();
        }

        if (!access.CanReadOrganization(httpContext.User, id))
        {
            return Results.Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeMemberIds = await db.Memberships
            .AsNoTracking()
            .Where(x => x.OrganizationId == id &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        x.EndDate == null)
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var degreeEvents = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => x.OrganizationId == id && activeMemberIds.Contains(x.MemberId))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.MemberId, x.Degree, x.EffectiveDate, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);

        var currentDegrees = degreeEvents
            .GroupBy(x => x.MemberId)
            .Select(group => group.First())
            .GroupBy(x => x.Degree)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var authorities = await db.OfficeAssignments
            .AsNoTracking()
            .Where(x => x.OrganizationId == id &&
                        x.StartDate <= today &&
                        (x.EndDate == null || x.EndDate >= today))
            .OrderBy(x => x.OfficeType)
            .ThenBy(x => x.Member.Person.LastNames)
            .ThenBy(x => x.Member.Person.FirstNames)
            .Select(x => new
            {
                x.Id,
                x.OfficeType,
                x.Period,
                x.MemberId,
                DisplayName = x.Member.Person.FirstNames + " " + x.Member.Person.LastNames,
                x.StartDate,
                x.EndDate
            })
            .ToListAsync(cancellationToken);

        var latestFinancial = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == id && x.MemberId == null)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.Status, x.AsOfDate, x.SourceReference })
            .FirstOrDefaultAsync(cancellationToken);

        var latestHospitalaria = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == id)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.Status, x.AsOfDate, x.SourceReference })
            .FirstOrDefaultAsync(cancellationToken);

        var meetings = await lodgeDb.LodgeMeetings
            .AsNoTracking()
            .Where(x => x.OrganizationId == id)
            .OrderByDescending(x => x.MeetingDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(8)
            .Select(x => new
            {
                x.Id,
                x.MeetingDate,
                x.MeetingType,
                x.Grade,
                x.Title,
                x.Status,
                x.ClosedAtUtc
            })
            .ToListAsync(cancellationToken);

        var instruction = await lodgeDb.LodgeInstructionSessions
            .AsNoTracking()
            .Where(x => x.OrganizationId == id)
            .OrderByDescending(x => x.InstructionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.InstructionDate,
                x.Grade,
                x.Topic,
                x.ResponsibleOffice,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var recentTransfers = await db.MemberTransfers
            .AsNoTracking()
            .Where(x => x.SourceOrganizationId == id || x.TargetOrganizationId == id)
            .OrderByDescending(x => x.RequestedDate)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                x.MemberId,
                MemberDisplayName = x.Member.Person.FirstNames + " " + x.Member.Person.LastNames,
                x.SourceOrganizationId,
                SourceOrganization = x.SourceOrganization.Name,
                x.TargetOrganizationId,
                TargetOrganization = x.TargetOrganization.Name,
                x.RequestedDate,
                x.ApprovedEffectiveDate,
                x.Status,
                Direction = x.SourceOrganizationId == id ? "outgoing" : "incoming"
            })
            .ToListAsync(cancellationToken);

        var canReadRegularity = OrganizationProfilePrivacy.CanReadRegularity(access, httpContext.User, id);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            organization,
            members = new
            {
                active = activeMemberIds.Count,
                degreeDistribution = currentDegrees
            },
            authorities,
            regularity = canReadRegularity ? new { financial = latestFinancial, hospitalaria = latestHospitalaria } : null,
            activity = new
            {
                recentMeetings = meetings,
                recentInstruction = instruction,
                recentTransfers
            }
        });
    }
}

public sealed record OrganizationOptionDto(
    Guid Id,
    string Name,
    string? Number,
    string Type);

public sealed record OrganizationOptionsResponse(
    int Total,
    IReadOnlyList<OrganizationOptionDto> Items);
