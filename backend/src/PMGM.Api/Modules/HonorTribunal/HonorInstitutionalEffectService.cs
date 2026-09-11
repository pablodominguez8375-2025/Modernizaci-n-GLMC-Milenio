using Microsoft.EntityFrameworkCore;

namespace PMGM.Api.Modules.HonorTribunal;

public interface IHonorInstitutionalEffectService
{
    Task<HonorInstitutionalEffect> ResolveAsync(Guid personId, DateOnly asOfDate, CancellationToken cancellationToken);
}

public sealed class HonorInstitutionalEffectService(HonorTribunalDbContext db) : IHonorInstitutionalEffectService
{
    public async Task<HonorInstitutionalEffect> ResolveAsync(
        Guid personId,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var sanctions = await db.HonorSanctions
            .AsNoTracking()
            .Where(x => x.PersonId == personId &&
                        x.Active &&
                        x.StartsOn <= asOfDate &&
                        (x.EndsOn == null || x.EndsOn >= asOfDate))
            .Select(x => new
            {
                x.Id,
                x.TotalLossOfRights,
                x.AffectsAttendance,
                x.AffectsVoting
            })
            .ToListAsync(cancellationToken);

        if (sanctions.Count == 0)
            return new HonorInstitutionalEffect(true, false, false, null);

        var hasRights = sanctions.All(x => !x.TotalLossOfRights);
        var attendanceRestricted = !hasRights || sanctions.Any(x => x.AffectsAttendance);
        var votingRestricted = !hasRights || sanctions.Any(x => x.AffectsVoting);
        var reference = string.Join(',', sanctions.Select(x => x.Id));

        return new HonorInstitutionalEffect(
            hasRights,
            attendanceRestricted,
            votingRestricted,
            reference);
    }
}
