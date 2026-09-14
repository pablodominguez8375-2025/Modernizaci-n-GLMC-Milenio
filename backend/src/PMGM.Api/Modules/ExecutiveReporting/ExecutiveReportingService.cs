using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.ExecutiveReporting;

public interface IExecutiveReportingService
{
    Task<ExecutiveReportDto> BuildAsync(DateOnly asOf, DateOnly from, CancellationToken cancellationToken);
}

public sealed class ExecutiveReportingService(
    PmgmDbContext db,
    LodgeManagementDbContext lodgeDb) : IExecutiveReportingService
{
    public async Task<ExecutiveReportDto> BuildAsync(
        DateOnly asOf,
        DateOnly from,
        CancellationToken cancellationToken)
    {
        var workshops = await db.Organizations
            .AsNoTracking()
            .Where(x => x.Type == "workshop")
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Name)
            .Select(x => new WorkshopIdentityRow(x.Id, x.Name, x.Number))
            .ToListAsync(cancellationToken);

        var workshopIds = workshops.Select(x => x.Id).ToList();
        var affiliations = workshopIds.Count == 0
            ? []
            : await db.Memberships
                .AsNoTracking()
                .Where(x => workshopIds.Contains(x.OrganizationId) &&
                            x.Status == MembershipCodes.MembershipStatus.Active &&
                            x.StartDate <= asOf &&
                            (x.EndDate == null || x.EndDate >= asOf))
                .Select(x => new AffiliationRow(x.MemberId, x.OrganizationId))
                .Distinct()
                .ToListAsync(cancellationToken);

        var currentMemberIds = affiliations.Select(x => x.MemberId).Distinct().ToList();

        var statusRows = currentMemberIds.Count == 0
            ? []
            : await db.InstitutionalStatusEvents
                .AsNoTracking()
                .Where(x => currentMemberIds.Contains(x.MemberId) && x.EffectiveDate <= asOf)
                .Select(x => new StatusRow(x.MemberId, x.OrganizationId, x.EventType, x.EffectiveDate, x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var periodStatusRows = statusRows
            .Where(x => x.EffectiveDate >= from && x.EffectiveDate <= asOf)
            .ToList();

        var degreeRows = currentMemberIds.Count == 0
            ? []
            : await db.DegreeEvents
                .AsNoTracking()
                .Where(x => currentMemberIds.Contains(x.MemberId) && x.EffectiveDate <= asOf)
                .Select(x => new DegreeRow(x.MemberId, x.Degree, x.EffectiveDate, x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var latestDegreeByMember = degreeRows
            .GroupBy(x => x.MemberId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.EffectiveDate)
                    .ThenByDescending(y => y.RecordedAtUtc)
                    .First().Degree);

        var pastOfficeholderMemberIds = currentMemberIds.Count == 0
            ? []
            : await db.OfficeAssignments
                .AsNoTracking()
                .Where(x => currentMemberIds.Contains(x.MemberId) &&
                            x.EndDate != null &&
                            x.EndDate < asOf)
                .Select(x => x.MemberId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var memberFinancialRows = currentMemberIds.Count == 0
            ? []
            : await db.FinancialRegularitySnapshots
                .AsNoTracking()
                .Where(x => x.MemberId != null &&
                            currentMemberIds.Contains(x.MemberId.Value) &&
                            workshopIds.Contains(x.OrganizationId) &&
                            x.AsOfDate <= asOf)
                .Select(x => new MemberFinancialRow(
                    x.MemberId!.Value,
                    x.OrganizationId,
                    x.Status,
                    x.AsOfDate,
                    x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var latestFinancialByAffiliation = memberFinancialRows
            .GroupBy(x => new { x.MemberId, x.OrganizationId })
            .Select(x => x.OrderByDescending(y => y.AsOfDate)
                .ThenByDescending(y => y.RecordedAtUtc)
                .First())
            .ToDictionary(x => (x.MemberId, x.OrganizationId));

        var workshopFinancialRows = workshopIds.Count == 0
            ? []
            : await db.FinancialRegularitySnapshots
                .AsNoTracking()
                .Where(x => x.MemberId == null && workshopIds.Contains(x.OrganizationId) && x.AsOfDate <= asOf)
                .Select(x => new WorkshopRegularityRow(x.OrganizationId, x.Status, x.AsOfDate, x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var latestWorkshopFinancial = workshopFinancialRows
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.AsOfDate)
                    .ThenByDescending(y => y.RecordedAtUtc)
                    .First());

        var hospitalariaRows = workshopIds.Count == 0
            ? []
            : await db.HospitalariaRegularitySnapshots
                .AsNoTracking()
                .Where(x => workshopIds.Contains(x.OrganizationId) && x.AsOfDate <= asOf)
                .Select(x => new WorkshopRegularityRow(x.OrganizationId, x.Status, x.AsOfDate, x.RecordedAtUtc))
                .ToListAsync(cancellationToken);

        var latestHospitalaria = hospitalariaRows
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.AsOfDate)
                    .ThenByDescending(y => y.RecordedAtUtc)
                    .First());

        var transfers = workshopIds.Count == 0
            ? []
            : await db.MemberTransfers
                .AsNoTracking()
                .Where(x => (workshopIds.Contains(x.SourceOrganizationId) || workshopIds.Contains(x.TargetOrganizationId)) &&
                            x.RequestedDate <= asOf)
                .Select(x => new TransferRow(
                    x.SourceOrganizationId,
                    x.TargetOrganizationId,
                    x.RequestedDate,
                    x.Status))
                .ToListAsync(cancellationToken);

        var meetings = workshopIds.Count == 0
            ? []
            : await lodgeDb.LodgeMeetings
                .AsNoTracking()
                .Where(x => workshopIds.Contains(x.OrganizationId) &&
                            x.MeetingDate >= from &&
                            x.MeetingDate <= asOf &&
                            x.Status != "cancelled")
                .Select(x => new ActivityRow(x.OrganizationId, x.MeetingDate))
                .ToListAsync(cancellationToken);

        var instruction = workshopIds.Count == 0
            ? []
            : await lodgeDb.LodgeInstructionSessions
                .AsNoTracking()
                .Where(x => workshopIds.Contains(x.OrganizationId) &&
                            x.InstructionDate >= from &&
                            x.InstructionDate <= asOf &&
                            x.Status != "cancelled")
                .Select(x => new ActivityRow(x.OrganizationId, x.InstructionDate))
                .ToListAsync(cancellationToken);

        var workshopRows = new List<ExecutiveWorkshopRowDto>(workshops.Count);
        foreach (var workshop in workshops)
        {
            var workshopAffiliations = affiliations
                .Where(x => x.OrganizationId == workshop.Id)
                .ToList();
            var memberIds = workshopAffiliations.Select(x => x.MemberId).Distinct().ToList();

            var active = 0;
            var inactive = 0;
            var blocking = 0;
            foreach (var memberId in memberIds)
            {
                var status = ResolveStatus(statusRows, memberId, workshop.Id);
                if (IsActive(status)) active++;
                else if (status == MembershipCodes.InstitutionalStatus.Inactive) inactive++;
                else if (IsBlocking(status)) blocking++;
            }

            var degreeDistribution = memberIds
                .Where(latestDegreeByMember.ContainsKey)
                .Select(memberId => latestDegreeByMember[memberId])
                .GroupBy(x => x)
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

            var delinquentAffiliations = workshopAffiliations.Count(x =>
                latestFinancialByAffiliation.TryGetValue((x.MemberId, x.OrganizationId), out var financial) &&
                financial.Status == TreasuryCodes.RegularityStatus.Delinquent);
            var affiliationsWithoutFinancialStatus = workshopAffiliations.Count(x =>
                !latestFinancialByAffiliation.ContainsKey((x.MemberId, x.OrganizationId)));

            latestWorkshopFinancial.TryGetValue(workshop.Id, out var workshopFinancial);
            latestHospitalaria.TryGetValue(workshop.Id, out var workshopHospitalaria);

            var periodTransfers = transfers
                .Where(x => x.RequestedDate >= from && x.RequestedDate <= asOf &&
                            (x.SourceOrganizationId == workshop.Id || x.TargetOrganizationId == workshop.Id))
                .ToList();
            var pendingTransfers = transfers.Count(x =>
                (x.SourceOrganizationId == workshop.Id || x.TargetOrganizationId == workshop.Id) &&
                x.Status is MembershipCodes.TransferStatus.Requested or MembershipCodes.TransferStatus.Approved);

            var flags = new List<string>();
            if (workshopFinancial?.Status == TreasuryCodes.RegularityStatus.Delinquent) flags.Add("treasury_delinquent");
            if (workshopHospitalaria?.Status == HospitalariaCodes.RegularityStatus.Overdue) flags.Add("hospitalaria_overdue");
            if (blocking > 0) flags.Add("blocking_member_status");
            if (pendingTransfers > 0) flags.Add("pending_transfers");

            workshopRows.Add(new ExecutiveWorkshopRowDto(
                workshop.Id,
                workshop.Name,
                workshop.Number,
                memberIds.Count,
                active,
                inactive,
                blocking,
                memberIds.Count(pastOfficeholderMemberIds.Contains),
                degreeDistribution,
                new ExecutiveEventCountersDto(
                    CountEvents(periodStatusRows, workshop.Id, MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal),
                    CountEvents(periodStatusRows, workshop.Id, MembershipCodes.InstitutionalStatus.ForcedWithdrawal),
                    CountEvents(periodStatusRows, workshop.Id, MembershipCodes.InstitutionalStatus.Reinstated),
                    CountEvents(periodStatusRows, workshop.Id, MembershipCodes.InstitutionalStatus.Deceased),
                    periodTransfers.Count(x => x.Status == MembershipCodes.TransferStatus.Executed)),
                new ExecutiveFinancialDto(
                    delinquentAffiliations,
                    affiliationsWithoutFinancialStatus,
                    workshopFinancial?.Status,
                    workshopFinancial?.AsOfDate,
                    workshopHospitalaria?.Status,
                    workshopHospitalaria?.AsOfDate),
                new ExecutiveActivityDto(
                    meetings.Count(x => x.OrganizationId == workshop.Id),
                    instruction.Count(x => x.OrganizationId == workshop.Id),
                    pendingTransfers),
                flags.Count > 0,
                flags));
        }

        var globalStatusByMember = currentMemberIds.ToDictionary(
            memberId => memberId,
            memberId => ResolveStatus(statusRows, memberId, null));

        var orderActive = globalStatusByMember.Values.Count(IsActive);
        var orderInactive = globalStatusByMember.Values.Count(x => x == MembershipCodes.InstitutionalStatus.Inactive);
        var orderBlocking = globalStatusByMember.Values.Count(IsBlocking);
        var delinquentMemberIds = latestFinancialByAffiliation.Values
            .Where(x => x.Status == TreasuryCodes.RegularityStatus.Delinquent)
            .Select(x => x.MemberId)
            .Distinct()
            .Count();
        var affiliationsWithoutStatus = affiliations.Count(x =>
            !latestFinancialByAffiliation.ContainsKey((x.MemberId, x.OrganizationId)));
        var periodTransfersExecuted = transfers.Count(x =>
            x.RequestedDate >= from && x.RequestedDate <= asOf &&
            x.Status == MembershipCodes.TransferStatus.Executed);
        var pendingTransfersOrder = transfers.Count(x =>
            x.Status is MembershipCodes.TransferStatus.Requested or MembershipCodes.TransferStatus.Approved);

        var overview = new ExecutiveOverviewDto(
            workshops.Count,
            currentMemberIds.Count,
            orderActive,
            orderInactive,
            orderBlocking,
            pastOfficeholderMemberIds.Count,
            new ExecutiveEventCountersDto(
                periodStatusRows.Count(x => x.Status == MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal),
                periodStatusRows.Count(x => x.Status == MembershipCodes.InstitutionalStatus.ForcedWithdrawal),
                periodStatusRows.Count(x => x.Status == MembershipCodes.InstitutionalStatus.Reinstated),
                periodStatusRows.Count(x => x.Status == MembershipCodes.InstitutionalStatus.Deceased),
                periodTransfersExecuted),
            new ExecutiveOrderFinancialDto(
                latestFinancialByAffiliation.Values.Count(x => x.Status == TreasuryCodes.RegularityStatus.Delinquent),
                delinquentMemberIds,
                affiliationsWithoutStatus,
                workshopRows.Count(x => x.Financial.WorkshopTreasuryStatus == TreasuryCodes.RegularityStatus.Delinquent),
                workshopRows.Count(x => x.Financial.WorkshopHospitalariaStatus == HospitalariaCodes.RegularityStatus.Overdue)),
            pendingTransfersOrder,
            workshopRows.Count(x => x.AttentionRequired));

        return new ExecutiveReportDto(
            asOf,
            new ExecutivePeriodDto(from, asOf),
            "Miembro actualmente afiliado que registra al menos un cargo institucional finalizado antes de la fecha de corte.",
            overview,
            workshopRows);
    }

    private static string ResolveStatus(
        IReadOnlyCollection<StatusRow> rows,
        Guid memberId,
        Guid? organizationId)
    {
        var candidates = rows.Where(x =>
            x.MemberId == memberId &&
            (organizationId is null || x.OrganizationId == null || x.OrganizationId == organizationId));

        return candidates
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => x.Status)
            .FirstOrDefault() ?? MembershipCodes.InstitutionalStatus.Active;
    }

    private static int CountEvents(
        IReadOnlyCollection<StatusRow> rows,
        Guid organizationId,
        string status)
        => rows.Count(x => x.OrganizationId == organizationId && x.Status == status);

    private static bool IsActive(string status)
        => status is MembershipCodes.InstitutionalStatus.Active or MembershipCodes.InstitutionalStatus.Reinstated;

    private static bool IsBlocking(string status)
        => status is MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal
            or MembershipCodes.InstitutionalStatus.ForcedWithdrawal
            or MembershipCodes.InstitutionalStatus.Deceased;

    private sealed record WorkshopIdentityRow(Guid Id, string Name, string? Number);
    private sealed record AffiliationRow(Guid MemberId, Guid OrganizationId);
    private sealed record StatusRow(Guid MemberId, Guid? OrganizationId, string Status, DateOnly EffectiveDate, DateTimeOffset RecordedAtUtc);
    private sealed record DegreeRow(Guid MemberId, string Degree, DateOnly EffectiveDate, DateTimeOffset RecordedAtUtc);
    private sealed record MemberFinancialRow(Guid MemberId, Guid OrganizationId, string Status, DateOnly AsOfDate, DateTimeOffset RecordedAtUtc);
    private sealed record WorkshopRegularityRow(Guid OrganizationId, string Status, DateOnly AsOfDate, DateTimeOffset RecordedAtUtc);
    private sealed record TransferRow(Guid SourceOrganizationId, Guid TargetOrganizationId, DateOnly RequestedDate, string Status);
    private sealed record ActivityRow(Guid OrganizationId, DateOnly Date);
}

public sealed record ExecutiveReportDto(
    DateOnly AsOf,
    ExecutivePeriodDto Period,
    string PastActiveDefinition,
    ExecutiveOverviewDto Overview,
    IReadOnlyList<ExecutiveWorkshopRowDto> Workshops);

public sealed record ExecutivePeriodDto(DateOnly From, DateOnly To);

public sealed record ExecutiveOverviewDto(
    int Workshops,
    int CurrentMembers,
    int ActiveMembers,
    int InactiveMembers,
    int BlockingMembers,
    int PastActive,
    ExecutiveEventCountersDto Events,
    ExecutiveOrderFinancialDto Financial,
    int PendingTransfers,
    int WorkshopsRequiringAttention);

public sealed record ExecutiveWorkshopRowDto(
    Guid OrganizationId,
    string Name,
    string? Number,
    int CurrentMembers,
    int ActiveMembers,
    int InactiveMembers,
    int BlockingMembers,
    int PastActive,
    IReadOnlyDictionary<string, int> DegreeDistribution,
    ExecutiveEventCountersDto Events,
    ExecutiveFinancialDto Financial,
    ExecutiveActivityDto Activity,
    bool AttentionRequired,
    IReadOnlyList<string> AttentionFlags);

public sealed record ExecutiveEventCountersDto(
    int VoluntaryWithdrawals,
    int ForcedWithdrawals,
    int Reinstatements,
    int Deaths,
    int Transfers);

public sealed record ExecutiveOrderFinancialDto(
    int DelinquentAffiliations,
    int DelinquentMembersDistinct,
    int AffiliationsWithoutStatus,
    int WorkshopsTreasuryDelinquent,
    int WorkshopsHospitalariaOverdue);

public sealed record ExecutiveFinancialDto(
    int DelinquentAffiliations,
    int AffiliationsWithoutStatus,
    string? WorkshopTreasuryStatus,
    DateOnly? WorkshopTreasuryAsOf,
    string? WorkshopHospitalariaStatus,
    DateOnly? WorkshopHospitalariaAsOf);

public sealed record ExecutiveActivityDto(
    int Meetings,
    int InstructionSessions,
    int PendingTransfers);
