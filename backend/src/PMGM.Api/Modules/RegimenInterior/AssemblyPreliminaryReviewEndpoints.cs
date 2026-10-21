using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Revisión preliminar de Asamblea: ninguna bandera de voto definitivo.
/// Solo identidades RI / Gran Secretaría autenticadas con alcance Orden.
/// </summary>
public static class AssemblyPreliminaryReviewEndpoints
{
    public static bool CanRead(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true &&
        user.HasClaim(InstitutionalClaims.Scope, "order") &&
        user.Claims.Any(c => c.Type == InstitutionalClaims.Role &&
            (c.Value == InstitutionalRoles.RegimenInterior ||
             c.Value == InstitutionalRoles.GranSecretaria)) &&
        !string.IsNullOrWhiteSpace(user.FindFirst("sub")?.Value);

    public static async Task<IResult> GetAsync(
        Guid assemblyId, HttpContext context,
        RegimenInteriorDbContext db, PmgmDbContext appDb,
        IAuditService audit, CancellationToken ct)
    {
        if (!CanRead(context.User))
            return Results.Forbid();
        if (assemblyId == Guid.Empty)
            return Results.BadRequest(new { code = "INVALID_ASSEMBLY_ID" });

        var assembly = await db.Set<AssemblyRecord>().AsNoTracking()
            .Where(x => x.Id == assemblyId)
            .Select(x => new { x.Id, x.Name, x.MeetingDate, x.CutoffDate, x.Status })
            .SingleOrDefaultAsync(ct);
        if (assembly is null)
            return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });

        var members = await db.AssemblyMemberStatuses.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId)
            .OrderBy(x => x.PersonId)
            .ToArrayAsync(ct);
        var decisions = await db.AssemblyEligibilities.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);

        var group = decisions.GroupBy(x => x.MemberStatusId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var rows = members.Select(m =>
            AssemblyPreliminaryReviewProjection.Project(
                m, group.GetValueOrDefault(m.Id) ?? [], assembly.CutoffDate))
            .ToArray();

        audit.Add(context, "regimen.asamblea.revision.consultada",
            "Assembly", assemblyId.ToString("D"), null, AuditResults.Success,
            new { MemberRows = rows.Length, AssemblyStatus = assembly.Status });
        await appDb.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            assemblyId = assembly.Id, assembly.Name,
            meetingDate = assembly.MeetingDate,
            cutoffDate = assembly.CutoffDate,
            status = assembly.Status,
            provisional = true,
            officiallyClosed = false,
            mayIssueBallots = false,
            items = rows
        });
    }
}

/// <summary>
/// Resuelve punta única de historial de decisiones sin reconstruir el voto.
/// No confirma que los conectores oficiales estén completos.
/// </summary>
public static class AssemblyPreliminaryReviewProjection
{
    public static AssemblyPreliminaryReviewRow Project(
        AssemblyMemberStatusRecord member,
        IReadOnlyCollection<AssemblyEligibilityRecord> history,
        DateOnly cutoff)
    {
        var linked = history.Where(x =>
            x.AssemblyId == member.AssemblyId &&
            x.MemberStatusId == member.Id).ToArray();

        var replacedIds = linked
            .Where(x => x.SupersedesEligibilityId is not null)
            .Select(x => x.SupersedesEligibilityId!.Value)
            .ToHashSet();
        var leaves = linked.Where(x => !replacedIds.Contains(x.Id)).ToArray();

        if (leaves.Length != 1 ||
            linked.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            return Pending(member, leaves.Length == 0 ?
                "NO_VALID_CURRENT_EVALUATION" : "CONFLICTING_EVALUATION_HISTORY");

        var evaluation = leaves[0];

        // Una punta única no demuestra integridad: puede existir una cadena
        // huérfana o un ciclo desconectado. Resolver hasta la raíz y exigir
        // que TODO el historial de esta persona participe de la misma cadena.
        var byId = linked.ToDictionary(x => x.Id);
        var visited = new HashSet<Guid>();
        var cursor = evaluation;
        while (true)
        {
            if (!visited.Add(cursor.Id))
                return Pending(member, "CONFLICTING_EVALUATION_HISTORY");
            if (cursor.SupersedesEligibilityId is not Guid parentId)
                break;
            if (parentId == Guid.Empty || !byId.TryGetValue(parentId, out var parent))
                return Pending(member, "INCOMPLETE_EVALUATION_HISTORY");
            if (parent.EvaluatedAtUtc > cursor.EvaluatedAtUtc)
                return Pending(member, "CONFLICTING_EVALUATION_HISTORY");
            cursor = parent;
        }
        if (visited.Count != linked.Length)
            return Pending(member, "INCOMPLETE_EVALUATION_HISTORY");

        if (evaluation.Status is not (
                AssemblyVoterEligibilityPolicy.Eligible or
                AssemblyVoterEligibilityPolicy.Ineligible or
                AssemblyVoterEligibilityPolicy.AttendanceOnly) ||
            string.IsNullOrWhiteSpace(evaluation.ReasonCode))
            return Pending(member, "EVALUATION_PENDING");

        if (evaluation.Status == AssemblyVoterEligibilityPolicy.Eligible &&
            (evaluation.MayVote != true || evaluation.MayAttend != true ||
             string.IsNullOrWhiteSpace(evaluation.GrandTreasurySourceReference) ||
             string.IsNullOrWhiteSpace(evaluation.GrandTreasuryCurrency) ||
             // Una referencia contable no acredita un monto: sin saldo cero
             // y sin acto de excepción RI verificado en esta proyección,
             // nunca afirmar habilitación provisional de sufragio.
             evaluation.GrandTreasuryPendingAmount != 0m ||
             evaluation.GrandTreasuryAsOfDate != cutoff ||
             string.IsNullOrWhiteSpace(evaluation.InstitutionalRightsSourceReference) ||
             string.IsNullOrWhiteSpace(evaluation.HonorRightsSourceReference)))
            return Pending(member, "ELIGIBILITY_SOURCES_UNVERIFIED");

        if (evaluation.Status != AssemblyVoterEligibilityPolicy.Eligible &&
            evaluation.MayVote)
            return Pending(member, "INCONSISTENT_VOTE_STATUS");

        return new AssemblyPreliminaryReviewRow(
            member.Id, member.PersonId, member.OrganizationId,
            member.Category, evaluation.Status,
            evaluation.MayAttend, evaluation.MayVote &&
                evaluation.Status == AssemblyVoterEligibilityPolicy.Eligible,
            false,
            evaluation.ReasonCode, evaluation.EvaluatedAtUtc);
    }

    private static AssemblyPreliminaryReviewRow Pending(
        AssemblyMemberStatusRecord member, string code) =>
        new(member.Id, member.PersonId, member.OrganizationId, member.Category,
            AssemblyVoterEligibilityPolicy.Pending, false, false, false,
            code, null);
}

public sealed record AssemblyPreliminaryReviewRow(
    Guid MemberStatusId,
    Guid PersonId,
    Guid OrganizationId,
    string Category,
    string Status,
    bool MayAttendPreliminary,
    bool MayVotePreliminary,
    bool MayVoteConfirmed,
    string ReasonCode,
    DateTimeOffset? EvaluatedAtUtc);
