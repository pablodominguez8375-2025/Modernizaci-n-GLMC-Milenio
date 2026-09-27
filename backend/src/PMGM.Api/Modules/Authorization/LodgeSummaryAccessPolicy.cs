using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Authorization;

public static class LodgeSummaryAccessPolicy
{
    public static async Task<bool> CanReadAsync(
        ClaimsPrincipal user,
        Guid organizationId,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        if (access.CanReadLodgeCouncilSummary(user, organizationId)) return true;

        var member = await memberContextResolver.ResolveAsync(user, cancellationToken);
        if (member is null || member.EffectiveDegree != 3) return false;

        var activeInOrganization = await db.Memberships.AsNoTracking().AnyAsync(x =>
            x.MemberId == member.MemberId &&
            x.OrganizationId == organizationId &&
            x.Status == MembershipCodes.MembershipStatus.Active &&
            x.EndDate == null, cancellationToken);
        if (!activeInOrganization) return false;

        return await db.LodgeSummaryAccessGrants.AsNoTracking().AnyAsync(x =>
            x.OrganizationId == organizationId &&
            x.MemberId == member.MemberId &&
            x.RevokedAtUtc == null, cancellationToken);
    }
}
