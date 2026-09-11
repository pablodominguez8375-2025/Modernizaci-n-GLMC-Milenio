using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Treasury;

public interface ITreasuryOrdinaryDuesEligibilityService
{
    Task<OrdinaryDuesEligibilityResponse?> GetAsync(
        Guid organizationId,
        Guid memberId,
        DateOnly asOf,
        CancellationToken cancellationToken);
}

public sealed class TreasuryOrdinaryDuesEligibilityService(PmgmDbContext db) : ITreasuryOrdinaryDuesEligibilityService
{
    public async Task<OrdinaryDuesEligibilityResponse?> GetAsync(
        Guid organizationId,
        Guid memberId,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var hasHistoricalMembership = await db.Memberships
            .AsNoTracking()
            .AnyAsync(x => x.MemberId == memberId && x.OrganizationId == organizationId, cancellationToken);

        if (!hasHistoricalMembership)
        {
            return null;
        }

        // Para una consulta histórica se usa el intervalo StartDate/EndDate y no el valor
        // terminal de Status, porque un segmento hoy cerrado pudo estar vigente en la fecha consultada.
        var hasMembershipSegment = await db.Memberships
            .AsNoTracking()
            .AnyAsync(x => x.MemberId == memberId &&
                           x.OrganizationId == organizationId &&
                           x.StartDate <= asOf &&
                           (x.EndDate == null || x.EndDate >= asOf),
                cancellationToken);

        var latestStatus = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => x.MemberId == memberId && x.EffectiveDate <= asOf)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.EventType, x.EffectiveDate })
            .FirstOrDefaultAsync(cancellationToken);

        var institutionalStatus = latestStatus?.EventType ??
                                  (hasMembershipSegment
                                      ? MembershipCodes.InstitutionalStatus.Active
                                      : MembershipCodes.InstitutionalStatus.Inactive);

        var generatesOrdinaryDues = InstitutionalStatusPolicy.GeneratesOrdinaryDues(
            institutionalStatus,
            hasMembershipSegment);

        return new OrdinaryDuesEligibilityResponse(
            organizationId,
            memberId,
            asOf,
            institutionalStatus,
            latestStatus?.EffectiveDate,
            hasMembershipSegment,
            generatesOrdinaryDues,
            ResolveReason(institutionalStatus, hasMembershipSegment, generatesOrdinaryDues));
    }

    private static string ResolveReason(string status, bool hasMembershipSegment, bool generatesOrdinaryDues)
    {
        if (generatesOrdinaryDues) return "ordinary_dues_applicable";
        if (InstitutionalStatusPolicy.IsPastActive(status)) return "past_active_exempt";
        if (InstitutionalStatusPolicy.IsVoluntaryWithdrawal(status)) return "voluntary_withdrawal_no_active_membership";
        if (!hasMembershipSegment) return "no_active_membership";
        return "institutional_status_not_billable";
    }
}

public sealed record OrdinaryDuesEligibilityResponse(
    Guid OrganizationId,
    Guid MemberId,
    DateOnly AsOf,
    string InstitutionalStatus,
    DateOnly? StatusEffectiveDate,
    bool HasMembershipSegment,
    bool GeneratesOrdinaryDues,
    string ReasonCode);
