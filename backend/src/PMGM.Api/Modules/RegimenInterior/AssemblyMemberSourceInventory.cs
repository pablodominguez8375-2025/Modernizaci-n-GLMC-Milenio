using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Inventario privado del registro CANÓNICO, previo a cualquier acreditación.
/// Ninguna OfficeAssignment demuestra un período V.M. completo; ninguna
/// Membership.Status demuestra por sí sola su estado histórico al corte.
/// NO exponer por HTTP ni alimentar directamente C22/C23 sin certificaciones.
/// </summary>
public static class AssemblyMemberSourceInventory
{
    public const int MaxOffices = 10000;

    public static async Task<AssemblyMemberSourceInventoryResult> DiscoverAsync(
        PmgmDbContext db, DateOnly cutoff, string canonicalMasterOfficeCode,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (cutoff == default || string.IsNullOrWhiteSpace(canonicalMasterOfficeCode) ||
            canonicalMasterOfficeCode.Length > 120)
            return Deny("INVALID_OFFICIAL_SOURCE_SELECTOR");

        // Consulta acotada y sin filtrar por EndDate; un V.M. que terminó su
        // cargo sigue siendo candidato a la categoría permanente.
        var offices = await db.OfficeAssignments.AsNoTracking()
            .Where(x => x.OfficeType == canonicalMasterOfficeCode &&
                        (x.StartDate == null || x.StartDate <= cutoff))
            .OrderBy(x => x.MemberId).ThenBy(x => x.StartDate)
            .Take(MaxOffices + 1).ToArrayAsync(ct);
        if (offices.Length == 0)
            return Deny("MASTER_OFFICE_RECORDS_MISSING");
        if (offices.Length > MaxOffices)
            return Deny("MASTER_OFFICE_SOURCE_LIMIT_EXCEEDED");
        if (offices.Any(x => x.MemberId == Guid.Empty ||
                x.OrganizationId == Guid.Empty || x.Id == Guid.Empty ||
                x.StartDate is null ||
                x.EndDate is not null && x.EndDate < x.StartDate ||
                string.IsNullOrWhiteSpace(x.EvidenceReference)) ||
            offices.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            return Deny("INCOMPLETE_MASTER_OFFICE_RECORDS");

        var memberIds = offices.Select(x => x.MemberId).Distinct().ToArray();
        var officeOrgIds = offices.Select(x => x.OrganizationId).Distinct().ToArray();
        var members = await db.Members.AsNoTracking().Include(x => x.Person)
            .Where(x => memberIds.Contains(x.Id)).ToArrayAsync(ct);
        var membership = await db.Memberships.AsNoTracking().Include(x => x.Organization)
            .Where(x => memberIds.Contains(x.MemberId) &&
                        (x.StartDate == null || x.StartDate <= cutoff) &&
                        (x.EndDate == null || x.EndDate >= cutoff))
            .ToArrayAsync(ct);
        var officeOrganizations = await db.Organizations.AsNoTracking()
            .Where(x => officeOrgIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var events = await db.InstitutionalStatusEvents.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.EffectiveDate <= cutoff)
            .ToArrayAsync(ct);

        // No asumir que un Taller soporte, una duplicación o un registro
        // posterior son representantes legítimos de la Asamblea.
        if (members.Length != memberIds.Length ||
            members.GroupBy(x => x.PersonId)
                .Any(g => g.Key == Guid.Empty || g.Count() > 1) ||
            officeOrganizations.Count != officeOrgIds.Length ||
            officeOrganizations.Values.Any(x => x.Type != "workshop") ||
            membership.Any(x => SupportWorkshopPolicy.IsSupportOrganization(x.Organization.Type)) ||
            memberIds.Any(id => membership.Count(x => x.MemberId == id) != 1) ||
            membership.Any(x => x.StartDate is null || x.Organization.Type != "workshop"))
            return Deny("MEMBERSHIP_OR_WORKSHOP_HISTORY_AMBIGUOUS");

        var byMember = members.ToDictionary(x => x.Id);
        var byMembership = membership.ToDictionary(x => x.MemberId);
        var candidates = new List<AssemblyUncertifiedMemberSource>(memberIds.Length);
        foreach (var id in memberIds.OrderBy(x => x))
        {
            var member = byMember[id];
            var current = byMembership[id];
            if (member.Person is null ||
                string.IsNullOrWhiteSpace(member.Person.FirstNames) ||
                string.IsNullOrWhiteSpace(member.Person.LastNames) ||
                string.IsNullOrWhiteSpace(current.Organization.Name) ||
                current.OrganizationId == Guid.Empty)
                return Deny("INCOMPLETE_MEMBER_IDENTITY");

            var latestStatus = events.Where(x => x.MemberId == id)
                .OrderByDescending(x => x.EffectiveDate)
                .ThenByDescending(x => x.RecordedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            candidates.Add(new AssemblyUncertifiedMemberSource(
                id, member.PersonId, current.OrganizationId,
                member.Person.FirstNames, member.Person.LastNames, member.Person.Rut,
                current.Organization.Name, current.Status,
                latestStatus?.EventType,
                offices.Where(x => x.MemberId == id).OrderBy(x => x.StartDate)
                    .Select(x => new AssemblyUncertifiedMasterOffice(
                        x.Id, x.OrganizationId, x.StartDate!.Value, x.EndDate,
                        x.EvidenceReference!)).ToArray()));
        }

        // Importante: la propiedad Certified permanece FALSE siempre.
        // Los hechos DB NO son constancias firmadas ni cobertura oficial.
        return new(true, false, candidates, Array.Empty<string>());
    }

    private static AssemblyMemberSourceInventoryResult Deny(string reason) =>
        new(false, false, [], [reason]);
}

public sealed record AssemblyUncertifiedMasterOffice(
    Guid OfficeAssignmentId, Guid HistoricalWorkshopId, DateOnly StartDate,
    DateOnly? EndDate, string RecordedEvidenceReference);

/// <summary>
/// Hechos privados de lectura; NO certificación de identidad/afiliación/VM,
/// NO bandera de voto y NO vista exportable del Padrón de Asamblea.
/// </summary>
public sealed record AssemblyUncertifiedMemberSource(
    Guid MemberId, Guid PersonId, Guid MembershipWorkshopId,
    string FirstNames, string LastNames, string? Rut, string WorkshopName,
    string CurrentRecordedMembershipStatus, string? LatestRecordedInstitutionalStatus,
    IReadOnlyList<AssemblyUncertifiedMasterOffice> MasterOffices);

public sealed record AssemblyMemberSourceInventoryResult(
    bool InventoryReadCompleted,
    bool InstitutionallyCertified,
    IReadOnlyList<AssemblyUncertifiedMemberSource> CandidateFacts,
    IReadOnlyList<string> BlockingReasons);
