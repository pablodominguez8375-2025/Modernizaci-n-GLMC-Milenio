using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Regla del PO (16-10): cualquier saldo pendiente del Taller con Gran Tesorería bloquea
/// el sufragio de sus asambleístas y SOLO Régimen Interior puede levantar el bloqueo.
/// Evento append-only 'lift'/'revoke' con motivo y evidencia, en la misma transacción
/// que la auditoría. No confirma sufragio ni cierra el padrón.
/// </summary>
public static class AssemblyDebtBlockEndpoints
{
    public const string Lift = "lift";
    public const string Revoke = "revoke";

    public static async Task<IResult> PostAsync(
        Guid assemblyId, Guid organizationId, AssemblyDebtBlockRequest request,
        HttpContext context, RegimenInteriorDbContext db, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        // Solo Régimen Interior (la Gran Secretaría puede leer, no decidir).
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.HasClaim(InstitutionalClaims.Scope, "order") ||
            !context.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior) ||
            string.IsNullOrWhiteSpace(context.User.FindFirst("sub")?.Value))
            return Results.Forbid();
        if (assemblyId == Guid.Empty || organizationId == Guid.Empty)
            return Results.BadRequest(new { code = "INVALID_ASSEMBLY_OR_WORKSHOP_ID" });

        var reason = request?.Reason?.Trim();
        var evidence = request?.EvidenceReference?.Trim();
        if (request is null || request.Action is not (Lift or Revoke) ||
            string.IsNullOrWhiteSpace(reason) || reason.Length is < 10 or > 2000 ||
            string.IsNullOrWhiteSpace(evidence) || evidence.Length > 500)
            return Results.BadRequest(new { code = "INVALID_DEBT_BLOCK_DECISION" });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var assembly = await db.Assemblies.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == assemblyId, ct);
        if (assembly is null)
            return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });
        if (assembly.Status != "draft" || assembly.ClosedAtUtc is not null)
            return Results.Conflict(new { code = "ASSEMBLY_NOT_EDITABLE" });

        var members = await db.AssemblyMemberStatuses.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId && x.OrganizationId == organizationId)
            .ToArrayAsync(ct);
        if (members.Length == 0)
            return Results.NotFound(new { code = "WORKSHOP_NOT_IN_ASSEMBLY" });

        var events = await db.AssemblyDebtDecisionEvents.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId && x.OrganizationId == organizationId)
            .ToArrayAsync(ct);
        var reversed = events.Where(e => e.Action == Revoke && e.ReversesEventId is not null)
            .Select(e => e.ReversesEventId!.Value).ToHashSet();
        var activeLift = events.Where(e => e.Action == Lift && !reversed.Contains(e.Id))
            .OrderByDescending(e => e.OccurredAtUtc).FirstOrDefault();

        Guid? reverses = null;
        if (request.Action == Lift)
        {
            if (activeLift is not null)
                return Results.Conflict(new { code = "DEBT_BLOCK_ALREADY_LIFTED" });
            var ids = members.Select(m => m.Id).ToArray();
            var history = await db.AssemblyEligibilities.AsNoTracking()
                .Where(x => x.AssemblyId == assemblyId && ids.Contains(x.MemberStatusId))
                .ToArrayAsync(ct);
            var byMember = history.GroupBy(h => h.MemberStatusId).ToDictionary(g => g.Key, g => g.ToArray());
            var hasDebtBlock = members.Any(m =>
            {
                var row = AssemblyPreliminaryReviewProjection.Project(
                    m, byMember.GetValueOrDefault(m.Id) ?? [], assembly.CutoffDate);
                return row.Status == AssemblyVoterEligibilityPolicy.AttendanceOnly &&
                       row.ReasonCode == AssemblyRosterListingBuilder.DebtReasonCode;
            });
            if (!hasDebtBlock)
                return Results.Conflict(new { code = "NO_DEBT_BLOCK_TO_LIFT" });
        }
        else
        {
            if (activeLift is null)
                return Results.Conflict(new { code = "NO_ACTIVE_LIFT_TO_REVOKE" });
            reverses = activeLift.Id;
        }

        var record = new AssemblyDebtDecisionEventRecord
        {
            AssemblyId = assemblyId,
            OrganizationId = organizationId,
            Action = request.Action,
            Authority = AssemblyVoterEligibilityPolicy.RegimenInteriorAuthority,
            DecidedBySubject = context.User.FindFirst("sub")!.Value,
            Reason = reason,
            EvidenceReference = evidence,
            ReversesEventId = reverses
        };
        db.AssemblyDebtDecisionEvents.Add(record);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg && pg.SqlState is "23505" or "23514" or "40001")
        {
            return Results.Conflict(new { code = "ASSEMBLY_CHANGED_RETRY_REVIEW" });
        }

        var audit = AuditEventFactory.Create(
            context, "regimen.asamblea.bloqueo_saldo." + request.Action,
            nameof(AssemblyDebtDecisionEventRecord), record.Id.ToString("D"),
            organizationId, AuditResults.Success,
            new { record.AssemblyId, record.Action, DecisionEventId = record.Id });
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO core.audit_events
              ("Id", "OccurredAtUtc", "Action", "EntityType", "EntityId",
               "OrganizationId", "ActorSubject", "ActorDisplayName", "IpAddress",
               "Menu", "Submenu", "Summary", "Result", "CorrelationId", "MetadataJson")
            VALUES
              ({audit.Id}, {audit.OccurredAtUtc}, {audit.Action}, {audit.EntityType},
               {audit.EntityId}, {audit.OrganizationId}, {audit.ActorSubject},
               {audit.ActorDisplayName}, {audit.IpAddress}, {audit.Menu},
               {audit.Submenu}, {audit.Summary}, {audit.Result},
               {audit.CorrelationId}, CAST({audit.MetadataJson} AS jsonb))
            """, ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            decisionEventId = record.Id, assemblyId, organizationId,
            action = record.Action, recordedAtUtc = record.OccurredAtUtc,
            provisional = true, mayVoteConfirmed = false, officiallyClosed = false
        });
    }
}

public sealed record AssemblyDebtBlockRequest(string? Action, string? Reason, string? EvidenceReference);
