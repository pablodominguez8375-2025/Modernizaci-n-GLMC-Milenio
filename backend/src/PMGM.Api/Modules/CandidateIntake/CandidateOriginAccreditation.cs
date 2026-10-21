using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// La acreditación la calcula el servidor, nunca el cliente. Aplica a
/// recomendantes de cualquier Taller de la Orden en la fecha de operación.
/// </summary>
public static class CandidateOriginAccreditation
{
    public static Task<CandidateOriginVerifiedMember?> VerifyAsync(
        PmgmDbContext db, Guid memberId, DateOnly operationDay, CancellationToken ct)
        => VerifyScopedAsync(db, memberId, null, operationDay, ct);

    /// <summary>
    /// La presentación/patrocinio institucional pertenece exclusivamente al
    /// Taller que tramita el expediente, no a cualquier Taller de la Orden.
    /// </summary>
    public static Task<CandidateOriginVerifiedMember?> VerifyWorkshopSponsorAsync(
        PmgmDbContext db, Guid memberId, Guid workshopId,
        DateOnly operationDay, CancellationToken ct)
        => workshopId == Guid.Empty ? Task.FromResult<CandidateOriginVerifiedMember?>(null)
           : VerifyScopedAsync(db, memberId, workshopId, operationDay, ct);

    private static async Task<CandidateOriginVerifiedMember?> VerifyScopedAsync(
        PmgmDbContext db, Guid memberId, Guid? requiredWorkshopId,
        DateOnly operationDay, CancellationToken ct)
    {
        if (memberId == Guid.Empty) return null;
        var membership = await db.Memberships.AsNoTracking()
            .Where(m => m.MemberId == memberId &&
                (requiredWorkshopId == null || m.OrganizationId == requiredWorkshopId) &&
                m.Status == MembershipCodes.MembershipStatus.Active &&
                (m.StartDate == null || m.StartDate <= operationDay) &&
                (m.EndDate == null || m.EndDate >= operationDay))
            .Select(m => new { m.Id }).FirstOrDefaultAsync(ct);
        if (membership is null) return null;

        var recognized = new[] {
            MembershipCodes.InstitutionalStatus.Active,
            MembershipCodes.InstitutionalStatus.Reinstated,
            MembershipCodes.InstitutionalStatus.Inactive,
            MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal,
            MembershipCodes.InstitutionalStatus.ForcedWithdrawal,
            MembershipCodes.InstitutionalStatus.Deceased
        };
        var latestStatus = await db.InstitutionalStatusEvents.AsNoTracking()
            .Where(x => x.MemberId == memberId &&
                x.EffectiveDate <= operationDay &&
                recognized.Contains(x.EventType))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => x.EventType)
            .FirstOrDefaultAsync(ct);
        var active = latestStatus is null or
            MembershipCodes.InstitutionalStatus.Active or
            MembershipCodes.InstitutionalStatus.Reinstated;
        return new CandidateOriginVerifiedMember(
            memberId, operationDay, active,
            "core.memberships:" + membership.Id.ToString("D"));
    }
}
