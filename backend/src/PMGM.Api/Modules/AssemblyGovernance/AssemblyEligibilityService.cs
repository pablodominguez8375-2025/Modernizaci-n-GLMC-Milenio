using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.AssemblyGovernance.Entities;
using PMGM.Api.Modules.HonorTribunal;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.AssemblyGovernance;

public interface IAssemblyEligibilityService
{
    Task<AssemblyEligibility> EvaluateAsync(
        Guid assemblyId,
        Guid personId,
        Guid lodgeId,
        string evaluatedBySubject,
        CancellationToken cancellationToken);
}

public sealed class AssemblyEligibilityService(
    AssemblyGovernanceDbContext assemblyDb,
    PmgmDbContext coreDb,
    IHonorInstitutionalEffectService honorEffects) : IAssemblyEligibilityService
{
    public async Task<AssemblyEligibility> EvaluateAsync(
        Guid assemblyId,
        Guid personId,
        Guid lodgeId,
        string evaluatedBySubject,
        CancellationToken cancellationToken)
    {
        var assembly = await assemblyDb.Assemblies
            .SingleOrDefaultAsync(x => x.Id == assemblyId, cancellationToken)
            ?? throw new InvalidOperationException("La Asamblea indicada no existe.");

        if (assembly.Status == AssemblyGovernanceCodes.AssemblyStatus.Frozen)
            throw new InvalidOperationException("El padrón de la Asamblea está cerrado y no puede recalcularse.");

        var asOfDate = assembly.AssemblyDate;
        var memberStatus = await assemblyDb.AssemblyMemberStatuses
            .AsNoTracking()
            .AnyAsync(x => x.PersonId == personId &&
                           x.LodgeId == lodgeId &&
                           x.Active &&
                           x.FromDate <= asOfDate &&
                           (x.ToDate == null || x.ToDate >= asOfDate),
                cancellationToken);

        var financialSnapshot = await coreDb.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == lodgeId &&
                        x.MemberId == null &&
                        x.Scope == TreasuryCodes.RegularityScope.Organization &&
                        x.AsOfDate <= asOfDate)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var treasuryUpToDate = financialSnapshot is not null &&
                               financialSnapshot.Status is TreasuryCodes.RegularityStatus.UpToDate or TreasuryCodes.RegularityStatus.Exempt;

        var restrictions = await assemblyDb.InstitutionalRestrictions
            .AsNoTracking()
            .Where(x => x.PersonId == personId &&
                        x.Active &&
                        x.StartDate <= asOfDate &&
                        (x.EndDate == null || x.EndDate >= asOfDate))
            .Select(x => new
            {
                x.Id,
                x.AffectsAttendance,
                x.AffectsVoting,
                x.AffectsMasonicRights
            })
            .ToListAsync(cancellationToken);

        var honor = await honorEffects.ResolveAsync(personId, asOfDate, cancellationToken);
        var hasMasonicRights = honor.HasMasonicRights && restrictions.All(x => !x.AffectsMasonicRights);
        var attendanceRestricted = honor.AttendanceRestricted || restrictions.Any(x => x.AffectsAttendance);
        var votingRestricted = honor.VotingRestricted || restrictions.Any(x => x.AffectsVoting);

        var decision = AssemblyEligibilityPolicy.Evaluate(new AssemblyEligibilityInput(
            memberStatus,
            treasuryUpToDate,
            attendanceRestricted,
            votingRestricted,
            hasMasonicRights));

        var now = DateTimeOffset.UtcNow;
        var eligibility = await assemblyDb.AssemblyEligibilities
            .SingleOrDefaultAsync(x => x.AssemblyId == assemblyId && x.PersonId == personId, cancellationToken);

        if (eligibility is null)
        {
            eligibility = new AssemblyEligibility
            {
                AssemblyId = assemblyId,
                PersonId = personId,
                LodgeId = lodgeId,
                Status = decision.Status,
                EvaluatedBySubject = evaluatedBySubject,
                EvaluatedAtUtc = now
            };
            assemblyDb.AssemblyEligibilities.Add(eligibility);
        }

        eligibility.LodgeId = lodgeId;
        eligibility.CanAttend = decision.CanAttend;
        eligibility.CanVote = decision.CanVote;
        eligibility.Status = decision.Status;
        eligibility.PrimaryReason = decision.PrimaryReason;
        eligibility.EvaluatedAtUtc = now;
        eligibility.EvaluatedBySubject = evaluatedBySubject;
        eligibility.FinancialSnapshotId = financialSnapshot?.Id;
        eligibility.RightsSnapshotReference = BuildRightsReference(restrictions.Select(x => x.Id), honor.EffectReference);
        eligibility.IsFrozenSnapshot = false;
        eligibility.FrozenAtUtc = null;

        await assemblyDb.SaveChangesAsync(cancellationToken);
        return eligibility;
    }

    private static string? BuildRightsReference(IEnumerable<Guid> restrictionIds, string? honorReference)
    {
        var parts = restrictionIds.Select(x => $"restriction:{x}").ToList();
        if (!string.IsNullOrWhiteSpace(honorReference))
            parts.Add($"honor-effect:{honorReference}");
        return parts.Count == 0 ? null : string.Join(';', parts);
    }
}
