using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Membership;

public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/members")
            .WithTags("Membership");

        group.MapGet("/{id:guid}/history", GetMemberHistoryAsync);
        return endpoints;
    }

    private static async Task<IResult> GetMemberHistoryAsync(
        Guid id,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        var member = await db.Members
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.PersonId,
                x.InstitutionalNumber,
                x.Person.FirstNames,
                x.Person.LastNames,
                x.Person.Email,
                x.CreatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return Results.NotFound();
        }

        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == id)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                Organization = x.Organization.Name,
                x.MembershipType,
                x.StartDate,
                x.EndDate,
                x.Status,
                x.EndReason,
                x.EvidenceReference
            })
            .ToListAsync(cancellationToken);

        var transfers = await db.MemberTransfers
            .AsNoTracking()
            .Where(x => x.MemberId == id)
            .OrderByDescending(x => x.RequestedDate)
            .Select(x => new
            {
                x.Id,
                x.SourceMembershipId,
                x.SourceOrganizationId,
                SourceOrganization = x.SourceOrganization.Name,
                x.TargetOrganizationId,
                TargetOrganization = x.TargetOrganization.Name,
                x.TargetMembershipId,
                x.RequestedDate,
                x.ProposedEffectiveDate,
                x.ApprovedEffectiveDate,
                x.Status,
                x.Reason,
                x.Resolution,
                x.EvidenceReference,
                x.ExecutedAtUtc
            })
            .ToListAsync(cancellationToken);

        var statusEvents = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => x.MemberId == id)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.EffectiveDate,
                x.RecordedAtUtc,
                x.Reason,
                x.OrganizationId,
                Organization = x.Organization == null ? null : x.Organization.Name,
                x.EvidenceReference,
                x.Notes
            })
            .ToListAsync(cancellationToken);

        var degreeEvents = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => x.MemberId == id)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Degree,
                x.EventType,
                x.EffectiveDate,
                x.OrganizationId,
                Organization = x.Organization.Name,
                x.EvidenceReference
            })
            .ToListAsync(cancellationToken);

        var offices = await db.OfficeAssignments
            .AsNoTracking()
            .Where(x => x.MemberId == id)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new
            {
                x.Id,
                x.OfficeType,
                x.Period,
                x.OrganizationId,
                Organization = x.Organization.Name,
                x.StartDate,
                x.EndDate,
                x.EvidenceReference
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            member,
            memberships,
            transfers,
            statusEvents,
            degreeEvents,
            offices
        });
    }
}
