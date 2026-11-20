using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 E2 — stages Person + Member + Membership rows INSIDE the caller's transaction.
/// Never commits, never touches the ledger or audit; the orchestrator owns those.
/// Fail-closed: an existing person is reused only when every compared field is identical
/// (names, email, degree, active membership in the same workshop); anything else is a
/// conflict and the caller must roll back the whole batch. No accounts, no roles.
/// </summary>
public static class InitialLoadMembersStep
{
    public const string StatusActive = "active";
    public const string Evidence = "initial_load";

    public sealed record Result(string? DenyCode, int Created, int ExactSkips)
    {
        public static Result Deny(string code) => new(code, 0, 0);
    }

    public static async Task<Result> StageAsync(
        PmgmDbContext core,
        IReadOnlyList<InitialLoadMemberRowPolicy.Validated> rows,
        IReadOnlyDictionary<string, Guid> organizationByWorkshopCode,
        CancellationToken ct)
    {
        int created = 0, skips = 0;
        foreach (var row in rows)
        {
            if (!organizationByWorkshopCode.TryGetValue(row.WorkshopCode, out var orgId))
                return Result.Deny("MEMBER_WORKSHOP_UNKNOWN");

            var person = await core.People.SingleOrDefaultAsync(x => x.Rut == row.Rut, ct);
            if (person is null)
            {
                var p = new Person { FirstNames = row.FirstNames, LastNames = row.LastNames, Rut = row.Rut, Email = row.Email };
                var m = new Member { Person = p, CurrentDegree = row.Grade };
                core.People.Add(p);
                core.Members.Add(m);
                core.Memberships.Add(new PMGM.Api.Modules.Membership.Entities.Membership
                {
                    Member = m, OrganizationId = orgId, MembershipType = row.MembershipType,
                    Status = StatusActive, EvidenceReference = Evidence
                });
                created++;
                continue;
            }

            var member = await core.Members.SingleOrDefaultAsync(x => x.PersonId == person.Id, ct);
            var identical = member is not null
                && Same(person.FirstNames, row.FirstNames) && Same(person.LastNames, row.LastNames)
                && Same(person.Email, row.Email)
                && string.Equals(member.CurrentDegree, row.Grade, StringComparison.Ordinal)
                && await core.Memberships.AnyAsync(x => x.MemberId == member.Id
                    && x.OrganizationId == orgId && x.Status == StatusActive, ct);
            if (!identical) return Result.Deny("MEMBER_CONFLICT_EXISTING_PERSON");
            skips++;
        }

        await core.SaveChangesAsync(ct);
        return new Result(null, created, skips);
    }

    private static bool Same(string? a, string? b) =>
        string.Equals(a?.Trim() ?? "", b?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
}
