using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.RegimenInterior;

public interface IRegimenInteriorMemberControlService
{
    Task<MemberControlResponse> QueryAsync(MemberControlQuery query, CancellationToken cancellationToken);
}

public sealed class RegimenInteriorMemberControlService(PmgmDbContext db) : IRegimenInteriorMemberControlService
{
    public async Task<MemberControlResponse> QueryAsync(MemberControlQuery query, CancellationToken cancellationToken)
    {
        var relatedMemberships = db.Memberships
            .AsNoTracking()
            .Where(x => x.StartDate <= query.AsOf);

        if (query.OrganizationId is not null)
        {
            relatedMemberships = relatedMemberships.Where(x => x.OrganizationId == query.OrganizationId.Value);
        }

        var relatedMemberIds = await relatedMemberships
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (relatedMemberIds.Count == 0)
        {
            return new MemberControlResponse(query.AsOf, 0, 0, []);
        }

        var people = await db.Members
            .AsNoTracking()
            .Where(x => relatedMemberIds.Contains(x.Id))
            .Select(x => new PersonRow(
                x.Id,
                x.InstitutionalNumber,
                x.Person.FirstNames + " " + x.Person.LastNames))
            .ToListAsync(cancellationToken);

        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(x => relatedMemberIds.Contains(x.MemberId) && x.StartDate <= query.AsOf)
            .Select(x => new MembershipRow(
                x.MemberId,
                x.OrganizationId,
                x.Organization.Name,
                x.Organization.Number,
                x.StartDate,
                x.EndDate,
                x.Status))
            .ToListAsync(cancellationToken);

        var statusEvents = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => relatedMemberIds.Contains(x.MemberId) && x.EffectiveDate <= query.AsOf)
            .Select(x => new StatusRow(
                x.MemberId,
                x.EventType,
                x.EffectiveDate,
                x.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        var degreeEvents = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => relatedMemberIds.Contains(x.MemberId) && x.EffectiveDate <= query.AsOf)
            .Select(x => new DegreeRow(
                x.MemberId,
                x.Degree,
                x.EventType,
                x.EffectiveDate,
                x.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        var transfers = await db.MemberTransfers
            .AsNoTracking()
            .Where(x => relatedMemberIds.Contains(x.MemberId) && x.RequestedDate <= query.AsOf)
            .Select(x => new TransferRow(
                x.MemberId,
                x.SourceOrganizationId,
                x.SourceOrganization.Name,
                x.TargetOrganizationId,
                x.TargetOrganization.Name,
                x.RequestedDate,
                x.ProposedEffectiveDate,
                x.ApprovedEffectiveDate,
                x.Status,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var financialRows = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.MemberId != null &&
                        relatedMemberIds.Contains(x.MemberId.Value) &&
                        x.AsOfDate <= query.AsOf)
            .Select(x => new FinancialRow(
                x.MemberId!.Value,
                x.OrganizationId,
                x.Status,
                x.AsOfDate,
                x.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        var rows = new List<MemberControlRow>(people.Count);
        foreach (var person in people)
        {
            var memberMemberships = memberships
                .Where(x => x.MemberId == person.MemberId)
                .OrderByDescending(x => x.StartDate)
                .ThenByDescending(x => x.EndDate)
                .ToList();

            var currentMemberships = memberMemberships
                .Where(x => IsCurrentMembership(x, query.AsOf))
                .OrderByDescending(x => x.StartDate)
                .ToList();
            var storedCurrentMembership = currentMemberships.FirstOrDefault();
            var lastMembership = storedCurrentMembership ?? memberMemberships.First();

            var memberStatuses = statusEvents
                .Where(x => x.MemberId == person.MemberId)
                .OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.RecordedAtUtc)
                .ToList();
            var latestStatus = memberStatuses.FirstOrDefault();
            var currentStatus = InstitutionalStatusPolicy.ResolveCurrentStatus(
                latestStatus?.Status,
                storedCurrentMembership is not null);

            // Past Activo y Retiro voluntario / En sueño se determinan exclusivamente
            // por el último estado institucional efectivo. Un traslado conserva estado
            // Activo si existe un segmento vigente en el Taller destino. Haber terminado
            // un cargo tampoco convierte a un Hermano en Past Activo.
            var pastActive = InstitutionalStatusPolicy.IsPastActive(currentStatus);
            var keepsWorkshopRosterMembership = storedCurrentMembership is not null &&
                                                InstitutionalStatusPolicy.KeepsWorkshopRosterMembership(currentStatus);
            var currentMembership = keepsWorkshopRosterMembership ? storedCurrentMembership : null;

            var memberDegrees = degreeEvents
                .Where(x => x.MemberId == person.MemberId)
                .OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.RecordedAtUtc)
                .ToList();
            var latestDegree = memberDegrees.FirstOrDefault();

            var memberTransfers = transfers
                .Where(x => x.MemberId == person.MemberId)
                .OrderByDescending(x => x.ApprovedEffectiveDate ?? x.ProposedEffectiveDate)
                .ThenByDescending(x => x.CreatedAtUtc)
                .ToList();
            var latestTransfer = memberTransfers.FirstOrDefault();
            var pendingTransfer = memberTransfers.Any(x =>
                x.Status is MembershipCodes.TransferStatus.Requested or MembershipCodes.TransferStatus.Approved);

            // La regularidad histórica no se borra al entrar en Past Activo o En sueño.
            // Se consulta contra la última membresía almacenada para conservar trazabilidad
            // de obligaciones anteriores; la generación de nuevas cuotas se rige por
            // InstitutionalStatusPolicy.GeneratesOrdinaryDues(...).
            var financial = financialRows
                .Where(x => x.MemberId == person.MemberId && x.OrganizationId == lastMembership.OrganizationId)
                .OrderByDescending(x => x.AsOfDate)
                .ThenByDescending(x => x.RecordedAtUtc)
                .FirstOrDefault();

            var withdrawal = memberStatuses
                .FirstOrDefault(x => x.Status is MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal or MembershipCodes.InstitutionalStatus.ForcedWithdrawal);
            var reinstatement = memberStatuses
                .FirstOrDefault(x => x.Status == MembershipCodes.InstitutionalStatus.Reinstated);
            var death = memberStatuses
                .FirstOrDefault(x => x.Status == MembershipCodes.InstitutionalStatus.Deceased);

            var relation = query.OrganizationId is null
                ? (currentMembership is null ? "historical" : "current")
                : currentMembership is not null && currentMembership.OrganizationId == query.OrganizationId.Value
                    ? "current"
                    : "historical";

            rows.Add(new MemberControlRow(
                person.MemberId,
                person.InstitutionalNumber,
                person.DisplayName,
                relation,
                currentMembership is null ? null : Workshop(currentMembership),
                Workshop(lastMembership),
                currentStatus,
                latestStatus?.EffectiveDate,
                latestDegree?.Degree,
                new MemberMilestones(
                    FirstDegreeDate(memberDegrees, MembershipCodes.DegreeEvent.Initiation),
                    FirstDegreeDate(memberDegrees, MembershipCodes.DegreeEvent.WageIncrease),
                    FirstDegreeDate(memberDegrees, MembershipCodes.DegreeEvent.Exaltation),
                    withdrawal?.Status,
                    withdrawal?.EffectiveDate,
                    reinstatement?.EffectiveDate,
                    death?.EffectiveDate,
                    latestTransfer is null ? null : latestTransfer.ApprovedEffectiveDate ?? latestTransfer.ProposedEffectiveDate),
                financial?.Status,
                pastActive,
                pendingTransfer,
                memberMemberships.Count));
        }

        IEnumerable<MemberControlRow> filtered = rows;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(x => string.Equals(x.CurrentStatus, query.Status, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(query.Degree))
        {
            filtered = filtered.Where(x => string.Equals(x.CurrentDegree, query.Degree, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(query.FinancialStatus))
        {
            filtered = query.FinancialStatus == "no_status"
                ? filtered.Where(x => x.FinancialStatus is null)
                : filtered.Where(x => string.Equals(x.FinancialStatus, query.FinancialStatus, StringComparison.OrdinalIgnoreCase));
        }
        if (query.PastActiveOnly)
        {
            filtered = filtered.Where(x => x.PastActive);
        }
        if (query.PendingTransferOnly)
        {
            filtered = filtered.Where(x => x.PendingTransfer);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = Normalize(query.Search);
            filtered = filtered.Where(x => Normalize($"{x.DisplayName} {x.InstitutionalNumber} {x.CurrentWorkshop?.Name} {x.LastWorkshop.Name}").Contains(term, StringComparison.Ordinal));
        }

        var ordered = filtered
            .OrderBy(x => x.DisplayName, StringComparer.Create(new System.Globalization.CultureInfo("es-CL"), ignoreCase: true))
            .ThenBy(x => x.InstitutionalNumber)
            .ToList();
        var total = ordered.Count;
        var items = ordered.Take(query.Limit).ToList();
        return new MemberControlResponse(query.AsOf, total, items.Count, items);
    }

    private static bool IsCurrentMembership(MembershipRow row, DateOnly asOf)
        => row.Status == MembershipCodes.MembershipStatus.Active &&
           row.StartDate <= asOf &&
           (row.EndDate == null || row.EndDate >= asOf);

    private static WorkshopRef Workshop(MembershipRow row)
        => new(row.OrganizationId, row.OrganizationName, row.OrganizationNumber, row.StartDate, row.EndDate);

    private static DateOnly? FirstDegreeDate(IReadOnlyCollection<DegreeRow> rows, string eventType)
        => rows.Where(x => x.EventType == eventType)
            .OrderBy(x => x.EffectiveDate)
            .ThenBy(x => x.RecordedAtUtc)
            .Select(x => (DateOnly?)x.EffectiveDate)
            .FirstOrDefault();

    private static string Normalize(string value)
        => string.Concat(value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant()
            .Trim();

    private sealed record PersonRow(Guid MemberId, string? InstitutionalNumber, string DisplayName);
    private sealed record MembershipRow(Guid MemberId, Guid OrganizationId, string OrganizationName, string? OrganizationNumber, DateOnly StartDate, DateOnly? EndDate, string Status);
    private sealed record StatusRow(Guid MemberId, string Status, DateOnly EffectiveDate, DateTimeOffset RecordedAtUtc);
    private sealed record DegreeRow(Guid MemberId, string Degree, string EventType, DateOnly EffectiveDate, DateTimeOffset RecordedAtUtc);
    private sealed record TransferRow(Guid MemberId, Guid SourceOrganizationId, string SourceOrganizationName, Guid TargetOrganizationId, string TargetOrganizationName, DateOnly RequestedDate, DateOnly ProposedEffectiveDate, DateOnly? ApprovedEffectiveDate, string Status, DateTimeOffset CreatedAtUtc);
    private sealed record FinancialRow(Guid MemberId, Guid OrganizationId, string Status, DateOnly AsOfDate, DateTimeOffset RecordedAtUtc);
}

public sealed record MemberControlQuery(
    DateOnly AsOf,
    Guid? OrganizationId,
    string? Status,
    string? Degree,
    string? FinancialStatus,
    string? Search,
    bool PastActiveOnly,
    bool PendingTransferOnly,
    int Limit);

public sealed record MemberControlResponse(
    DateOnly AsOf,
    int Total,
    int Returned,
    IReadOnlyList<MemberControlRow> Items);

public sealed record MemberControlRow(
    Guid MemberId,
    string? InstitutionalNumber,
    string DisplayName,
    string Relation,
    WorkshopRef? CurrentWorkshop,
    WorkshopRef LastWorkshop,
    string CurrentStatus,
    DateOnly? StatusEffectiveDate,
    string? CurrentDegree,
    MemberMilestones Milestones,
    string? FinancialStatus,
    bool PastActive,
    bool PendingTransfer,
    int MembershipHistoryCount);

public sealed record WorkshopRef(
    Guid Id,
    string Name,
    string? Number,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record MemberMilestones(
    DateOnly? Initiation,
    DateOnly? WageIncrease,
    DateOnly? Exaltation,
    string? WithdrawalType,
    DateOnly? Withdrawal,
    DateOnly? Reinstatement,
    DateOnly? Death,
    DateOnly? Transfer);
