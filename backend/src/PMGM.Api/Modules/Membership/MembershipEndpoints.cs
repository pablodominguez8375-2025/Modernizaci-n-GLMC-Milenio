using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Membership;

public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/members")
            .WithTags("Membership")
            .RequireAuthorization();

        group.MapGet("", GetMembersAsync);
        group.MapGet("/{id:guid}/profile", GetMemberProfileAsync);
        group.MapGet("/{id:guid}/history", GetMemberHistoryAsync);
        return endpoints;
    }

    private static async Task<IResult> GetMembersAsync(
        Guid organizationId,
        string? query,
        string? status,
        int? limit,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Debe indicar el Taller u organización." });
        }

        var organization = await db.Organizations
            .AsNoTracking()
            .Where(x => x.Id == organizationId)
            .Select(x => new { x.Id, x.Name, x.Number, x.Type })
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return Results.NotFound();
        }

        if (!access.CanReadOrganization(httpContext.User, organizationId))
        {
            return Results.Forbid();
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            status is not MembershipCodes.MembershipStatus.Active and
            not MembershipCodes.MembershipStatus.Transferred and
            not MembershipCodes.MembershipStatus.Closed)
        {
            return Results.BadRequest(new { message = "Estado de membresía no válido." });
        }

        var take = Math.Clamp(limit ?? 100, 1, 250);
        var normalizedQuery = query?.Trim();

        var latestMemberships = db.Memberships
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .GroupBy(x => x.MemberId)
            .Select(group => group
                .OrderByDescending(x => x.StartDate)
                .ThenByDescending(x => x.CreatedAtUtc)
                .First());

        if (!string.IsNullOrWhiteSpace(status))
        {
            latestMemberships = latestMemberships.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var pattern = $"%{normalizedQuery}%";
            latestMemberships = latestMemberships.Where(x =>
                EF.Functions.ILike(x.Member.Person.FirstNames, pattern) ||
                EF.Functions.ILike(x.Member.Person.LastNames, pattern) ||
                EF.Functions.ILike(x.Member.Person.FirstNames + " " + x.Member.Person.LastNames, pattern) ||
                (x.Member.InstitutionalNumber != null && EF.Functions.ILike(x.Member.InstitutionalNumber, pattern)));
        }

        var total = await latestMemberships.CountAsync(cancellationToken);
        var items = await latestMemberships
            .OrderBy(x => x.Member.Person.LastNames)
            .ThenBy(x => x.Member.Person.FirstNames)
            .Take(take)
            .Select(x => new
            {
                x.MemberId,
                DisplayName = x.Member.Person.FirstNames + " " + x.Member.Person.LastNames,
                x.Member.InstitutionalNumber,
                MembershipStatus = x.Status,
                x.MembershipType,
                x.StartDate,
                x.EndDate,
                CurrentDegree = db.DegreeEvents
                    .Where(degree => degree.MemberId == x.MemberId)
                    .OrderByDescending(degree => degree.EffectiveDate)
                    .ThenByDescending(degree => degree.RecordedAtUtc)
                    .Select(degree => degree.Degree)
                    .FirstOrDefault(),
                InstitutionalStatus = db.InstitutionalStatusEvents
                    .Where(statusEvent => statusEvent.MemberId == x.MemberId)
                    .OrderByDescending(statusEvent => statusEvent.EffectiveDate)
                    .ThenByDescending(statusEvent => statusEvent.RecordedAtUtc)
                    .Select(statusEvent => statusEvent.EventType)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            organization,
            total,
            returned = items.Count,
            items
        });
    }

    private static async Task<IResult> GetMemberProfileAsync(
        Guid id,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
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
                x.CreatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return Results.NotFound();
        }

        var memberOrganizationIds = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == id)
            .Select(x => x.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var hasOrderHistoryAccess = HasOrderHistoryAccess(access, httpContext.User);
        var allowedOrganizationIds = hasOrderHistoryAccess
            ? memberOrganizationIds
            : memberOrganizationIds
                .Where(x => access.CanReadOrganization(httpContext.User, x))
                .ToList();

        if (!hasOrderHistoryAccess && allowedOrganizationIds.Count == 0)
        {
            return Results.Forbid();
        }

        var canReadContact = MemberProfilePrivacy.CanReadContact(access, httpContext.User, allowedOrganizationIds);
        object? contact = null;
        if (canReadContact)
        {
            contact = await db.People
                .AsNoTracking()
                .Where(x => x.Id == member.PersonId)
                .Select(x => new { x.Email, x.Phone, x.Address })
                .SingleAsync(cancellationToken);
        }

        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess || allowedOrganizationIds.Contains(x.OrganizationId)))
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                Organization = x.Organization.Name,
                OrganizationNumber = x.Organization.Number,
                OrganizationType = x.Organization.Type,
                x.MembershipType,
                x.StartDate,
                x.EndDate,
                x.Status,
                x.EndReason,
                x.EvidenceReference
            })
            .ToListAsync(cancellationToken);

        var degreeEvents = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess || allowedOrganizationIds.Contains(x.OrganizationId)))
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
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess || allowedOrganizationIds.Contains(x.OrganizationId)))
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

        var statusEvents = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess ||
                         x.OrganizationId == null ||
                         allowedOrganizationIds.Contains(x.OrganizationId.Value)))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Take(30)
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

        var transfers = await db.MemberTransfers
            .AsNoTracking()
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess ||
                         allowedOrganizationIds.Contains(x.SourceOrganizationId) ||
                         allowedOrganizationIds.Contains(x.TargetOrganizationId)))
            .OrderByDescending(x => x.RequestedDate)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.SourceOrganizationId,
                SourceOrganization = x.SourceOrganization.Name,
                x.TargetOrganizationId,
                TargetOrganization = x.TargetOrganization.Name,
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

        var currentMembership = memberships
            .FirstOrDefault(x => x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null)
            ?? memberships.FirstOrDefault();
        var currentDegree = degreeEvents.FirstOrDefault();
        var currentStatus = statusEvents.FirstOrDefault();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentOffices = offices
            .Where(x => x.StartDate <= today && (x.EndDate == null || x.EndDate >= today))
            .ToList();

        object? regularity = null;
        if (access.CanReadInstitutionalRegularity(httpContext.User))
        {
            var financial = await db.FinancialRegularitySnapshots
                .AsNoTracking()
                .Where(x => x.MemberId == id)
                .OrderByDescending(x => x.AsOfDate)
                .ThenByDescending(x => x.RecordedAtUtc)
                .Select(x => new { x.Status, x.AsOfDate, x.Scope, x.SourceReference })
                .FirstOrDefaultAsync(cancellationToken);

            var hospitalaria = currentMembership is null
                ? null
                : await db.HospitalariaRegularitySnapshots
                    .AsNoTracking()
                    .Where(x => x.OrganizationId == currentMembership.OrganizationId)
                    .OrderByDescending(x => x.AsOfDate)
                    .ThenByDescending(x => x.RecordedAtUtc)
                    .Select(x => new { x.Status, x.AsOfDate, x.SourceReference })
                    .FirstOrDefaultAsync(cancellationToken);

            regularity = new { financial, hospitalaria };
        }

        return Results.Ok(new
        {
            member,
            contactVisible = canReadContact,
            contact,
            scope = hasOrderHistoryAccess ? "order" : "authorized_organizations",
            current = new
            {
                membership = currentMembership,
                degree = currentDegree,
                institutionalStatus = currentStatus,
                offices = currentOffices
            },
            memberships,
            degreeEvents,
            offices,
            statusEvents,
            transfers,
            regularity
        });
    }

    private static async Task<IResult> GetMemberHistoryAsync(
        Guid id,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
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
                x.CreatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return Results.NotFound();
        }

        var memberOrganizationIds = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == id)
            .Select(x => x.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var hasOrderHistoryAccess = HasOrderHistoryAccess(access, httpContext.User);
        var allowedOrganizationIds = hasOrderHistoryAccess
            ? memberOrganizationIds
            : memberOrganizationIds
                .Where(x => access.CanReadOrganization(httpContext.User, x))
                .ToList();

        if (!hasOrderHistoryAccess && allowedOrganizationIds.Count == 0)
        {
            return Results.Forbid();
        }

        var canReadContact = MemberProfilePrivacy.CanReadContact(access, httpContext.User, allowedOrganizationIds);
        object? contact = null;
        if (canReadContact)
        {
            contact = await db.People
                .AsNoTracking()
                .Where(x => x.Id == member.PersonId)
                .Select(x => new { x.Email, x.Phone, x.Address })
                .SingleAsync(cancellationToken);
        }

        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess || allowedOrganizationIds.Contains(x.OrganizationId)))
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
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess ||
                         allowedOrganizationIds.Contains(x.SourceOrganizationId) ||
                         allowedOrganizationIds.Contains(x.TargetOrganizationId)))
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
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess ||
                         x.OrganizationId == null ||
                         allowedOrganizationIds.Contains(x.OrganizationId.Value)))
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
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess || allowedOrganizationIds.Contains(x.OrganizationId)))
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
            .Where(x => x.MemberId == id &&
                        (hasOrderHistoryAccess || allowedOrganizationIds.Contains(x.OrganizationId)))
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
            contactVisible = canReadContact,
            contact,
            scope = hasOrderHistoryAccess ? "order" : "authorized_organizations",
            memberships,
            transfers,
            statusEvents,
            degreeEvents,
            offices
        });
    }

    private static bool HasOrderHistoryAccess(IInstitutionalAccessService access, System.Security.Claims.ClaimsPrincipal user)
        => access.HasOrderScope(user) &&
           access.HasRole(
               user,
               InstitutionalRoles.GranLogiaAdmin,
               InstitutionalRoles.RegimenInterior);
}
