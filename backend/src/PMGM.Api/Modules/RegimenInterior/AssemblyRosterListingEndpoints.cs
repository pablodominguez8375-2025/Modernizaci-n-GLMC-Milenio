using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Listados PRELIMINARES de la Gran Asamblea para Régimen Interior / Gran Secretaría:
/// asambleístas y sufragantes habilitados, con RUT enmascarado. Nunca es el
/// padrón cerrado ni autoriza votar (el cierre sigue bloqueado por
/// <see cref="AssemblyClosurePreflightPolicy"/>).
/// </summary>
public static class AssemblyRosterListingEndpoints
{
    public static async Task<IResult> GetAsync(
        Guid assemblyId, HttpContext context,
        RegimenInteriorDbContext db, PmgmDbContext appDb,
        IAuditService audit, CancellationToken ct)
    {
        if (!AssemblyPreliminaryReviewEndpoints.CanRead(context.User))
            return Results.Forbid();
        if (assemblyId == Guid.Empty)
            return Results.BadRequest(new { code = "INVALID_ASSEMBLY_ID" });
        context.Response.Headers.CacheControl = "private, no-store";

        var assembly = await db.Assemblies.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == assemblyId, ct);
        if (assembly is null)
            return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });

        var members = await db.AssemblyMemberStatuses.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId)
            .OrderBy(x => x.Id).Take(10001).ToArrayAsync(ct);
        if (members.Length > 10000)
            return Results.Conflict(new { code = "ASSEMBLY_TOO_LARGE" });
        var history = await db.AssemblyEligibilities.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);
        var debtEvents = await db.AssemblyDebtDecisionEvents.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);

        var listing = AssemblyRosterListingBuilder.Build(assembly, members, history, debtEvents);

        audit.Add(context, "regimen.asamblea.listados.consultados",
            nameof(AssemblyRecord), assemblyId.ToString("D"), null, AuditResults.Success,
            new { Asambleistas = listing.Asambleistas.Count, Sufragantes = listing.Sufragantes.Count });
        await appDb.SaveChangesAsync(ct);
        return Results.Ok(listing);
    }
}

/// <summary>Proyección pura (testeable sin base de datos) de los dos listados.</summary>
public static class AssemblyRosterListingBuilder
{
    public const string DebtReasonCode = "MOROSIDAD_GRAN_TESORERIA";

    public static AssemblyRosterListing Build(
        AssemblyRecord assembly,
        IReadOnlyCollection<AssemblyMemberStatusRecord> members,
        IReadOnlyCollection<AssemblyEligibilityRecord> history,
        IReadOnlyCollection<AssemblyDebtDecisionEventRecord> debtEvents)
    {
        var byMember = history.GroupBy(h => h.MemberStatusId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AssemblyEligibilityRecord>)g.ToArray());
        var lifted = LiftedOrganizations(debtEvents);

        var candidates = new List<AssemblyRosterCandidate>();
        var extra = new Dictionary<Guid, (AssemblyMemberStatusRecord Member, AssemblyPreliminaryReviewRow Row, bool Blocked, bool Lifted, bool MayVote)>();
        foreach (var m in members)
        {
            var row = AssemblyPreliminaryReviewProjection.Project(
                m, byMember.GetValueOrDefault(m.Id) ?? [], assembly.CutoffDate);
            var blocked = row.Status == AssemblyVoterEligibilityPolicy.AttendanceOnly &&
                          row.ReasonCode == DebtReasonCode;
            var isLifted = blocked && lifted.Contains(m.OrganizationId);
            var mayVote = (row.MayVotePreliminary && row.Status == AssemblyVoterEligibilityPolicy.Eligible) || isLifted;
            var status = isLifted ? AssemblyVoterEligibilityPolicy.Eligible : row.Status;
            var decision = new AssemblyEligibilityDecision(status, row.MayAttendPreliminary || isLifted, mayVote, row.ReasonCode);
            candidates.Add(new AssemblyRosterCandidate(m.PersonId, m.LastNamesAtCutoff, m.FirstNamesAtCutoff,
                AssemblyRutMask.Mask(m.RutAtCutoff), m.WorkshopAtCutoff, decision));
            extra[m.PersonId] = (m, row, blocked, isLifted, mayVote);
        }

        var asambleistas = AssemblyRosterPolicy.Order(candidates).Select((c, i) =>
        {
            var e = extra[c.PersonId];
            return new AssemblyListingRow(i + 1, c.PersonId, e.Member.OrganizationId,
                string.Concat(c.LastNames.Trim(), ", ", c.FirstNames.Trim()), c.Rut, c.Workshop,
                e.Member.Category, c.Eligibility.Status, c.Eligibility.MayAttend, e.MayVote,
                e.Blocked, e.Lifted, c.Eligibility.ReasonCode, "");
        }).ToArray();

        var sufragantes = asambleistas.Where(r => r.MayVote)
            .Select((r, i) => new AssemblyVoterRow(i + 1, r.PersonId, r.Name, r.Rut, r.Workshop, ""))
            .ToArray();

        var talleres = asambleistas.GroupBy(r => r.OrganizationId)
            .Select(g => new AssemblyWorkshopDebtRow(g.Key, g.First().Workshop, g.Count(),
                g.Count(x => x.BlockedByDebt || x.BlockLifted), g.Any(x => x.BlockLifted)))
            .Where(t => t.Affected > 0)
            .OrderBy(t => t.Workshop, StringComparer.CurrentCultureIgnoreCase).ToArray();

        return new AssemblyRosterListing(assembly.Id, assembly.Name, assembly.MeetingDate,
            assembly.CutoffDate, assembly.Status, assembly.Status != "closed", assembly.Status == "closed" && assembly.ClosedAtUtc is not null, false, asambleistas, sufragantes, talleres);
    }

    /// <summary>Talleres con un 'lift' vigente (no revertido por un 'revoke' posterior).</summary>
    public static HashSet<Guid> LiftedOrganizations(IEnumerable<AssemblyDebtDecisionEventRecord> events)
    {
        var all = events.ToArray();
        var reversed = all.Where(e => e.Action == "revoke" && e.ReversesEventId is not null)
            .Select(e => e.ReversesEventId!.Value).ToHashSet();
        return all.Where(e => e.Action == "lift" && !reversed.Contains(e.Id))
            .Select(e => e.OrganizationId).ToHashSet();
    }
}

public static class AssemblyRutMask
{
    /// <summary>12345678-5 → 12.345.***-5. Nunca devuelve el cuerpo completo.</summary>
    public static string Mask(string? rut)
    {
        var clean = (rut ?? "").Replace(".", "").Replace(" ", "").Trim();
        var parts = clean.Split('-');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length != 1 ||
            !parts[0].All(char.IsDigit))
            return "***";
        var body = parts[0];
        if (body.Length <= 3) return "***-" + parts[1];
        var visible = body[..^3];
        var grouped = new System.Text.StringBuilder();
        for (var i = 0; i < visible.Length; i++)
        {
            if (i > 0 && (visible.Length - i) % 3 == 0) grouped.Append('.');
            grouped.Append(visible[i]);
        }
        return $"{grouped}.***-{parts[1]}";
    }
}

public sealed record AssemblyListingRow(
    int Number, Guid PersonId, Guid OrganizationId, string Name, string Rut, string Workshop,
    string Category, string Status, bool MayAttend, bool MayVote,
    bool BlockedByDebt, bool BlockLifted, string ReasonCode, string Signature);

public sealed record AssemblyVoterRow(
    int Number, Guid PersonId, string Name, string Rut, string Workshop, string Signature);

public sealed record AssemblyWorkshopDebtRow(
    Guid OrganizationId, string Workshop, int Members, int Affected, bool BlockLifted);

public sealed record AssemblyRosterListing(
    Guid AssemblyId, string Name, DateOnly MeetingDate, DateOnly CutoffDate, string Status,
    bool Provisional, bool OfficiallyClosed, bool MayIssueBallots,
    IReadOnlyList<AssemblyListingRow> Asambleistas,
    IReadOnlyList<AssemblyVoterRow> Sufragantes,
    IReadOnlyList<AssemblyWorkshopDebtRow> TalleresConBloqueo);
