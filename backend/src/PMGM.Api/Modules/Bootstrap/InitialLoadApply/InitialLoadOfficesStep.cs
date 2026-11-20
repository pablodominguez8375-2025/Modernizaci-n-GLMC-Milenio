using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 E3 — stages OfficeAssignment rows inside the caller's transaction (no commit, no ledger).
/// officeCode must match EXACTLY one active OfficeDefinition whose OrganizationType is the
/// workshop type; the person must be a member of THIS batch's Hermanos sheet in the same workshop.
/// An existing assignment is an exact skip only if period and startDate are identical; same
/// member/workshop/office/period with different data is a conflict. Anything else denies the batch.
/// </summary>
public static class InitialLoadOfficesStep
{
    public sealed record Result(string? DenyCode, int Created, int ExactSkips)
    {
        public static Result Deny(string code) => new(code, 0, 0);
    }

    public static async Task<Result> StageAsync(
        PmgmDbContext core,
        BootstrapDbContext boot,
        IReadOnlyList<InitialLoadOfficeRowPolicy.Validated> offices,
        IReadOnlyList<InitialLoadMemberRowPolicy.Validated> members,
        IReadOnlyDictionary<string, Guid> organizationByWorkshopCode,
        CancellationToken ct)
    {
        int created = 0, skips = 0;
        var memberByRef = members.ToDictionary(x => x.PersonRef, StringComparer.Ordinal);
        foreach (var row in offices)
        {
            if (!memberByRef.TryGetValue(row.PersonRef, out var m)) return Result.Deny("OFFICE_PERSON_UNKNOWN");
            if (!string.Equals(m.WorkshopCode, row.WorkshopCode, StringComparison.Ordinal))
                return Result.Deny("OFFICE_MEMBER_WORKSHOP_MISMATCH");
            if (!organizationByWorkshopCode.TryGetValue(row.WorkshopCode, out var orgId))
                return Result.Deny("OFFICE_WORKSHOP_UNKNOWN");

            var defs = await boot.OfficeDefinitions.AsNoTracking()
                .Where(x => x.Code == row.OfficeCode && x.IsActive
                    && x.OrganizationType == BootstrapCodes.WorkshopType)
                .Take(2).ToListAsync(ct);
            if (defs.Count != 1) return Result.Deny("OFFICE_CODE_NOT_EXACT");

            var memberId = await core.Members.Where(x => x.Person.Rut == m.Rut)
                .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (memberId is null) return Result.Deny("OFFICE_MEMBER_MISSING");

            var existing = await core.OfficeAssignments.Where(x => x.MemberId == memberId
                && x.OrganizationId == orgId && x.OfficeType == defs[0].Code && x.Period == row.Period)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                if (existing.Count == 1 && existing[0].StartDate == row.StartDate && existing[0].EndDate is null)
                { skips++; continue; }
                return Result.Deny("OFFICE_CONFLICT_EXISTING_ASSIGNMENT");
            }

            core.OfficeAssignments.Add(new OfficeAssignment
            {
                MemberId = memberId.Value, OrganizationId = orgId, OfficeType = defs[0].Code,
                Period = row.Period, StartDate = row.StartDate, EvidenceReference = InitialLoadMembersStep.Evidence
            });
            created++;
        }
        await core.SaveChangesAsync(ct);
        return new Result(null, created, skips);
    }
}
