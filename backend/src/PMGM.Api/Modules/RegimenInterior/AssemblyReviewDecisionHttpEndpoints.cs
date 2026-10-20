using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Registra únicamente gestiones preliminares: nunca confirma sufragio o cierre.
/// La decisión y su bitácora se escriben en la MISMA transacción PostgreSQL.
/// </summary>
public static class AssemblyReviewDecisionHttpEndpoints
{
    public static async Task<IResult> PostAsync(
        Guid assemblyId,
        Guid personId,
        AssemblyReviewDecisionHttpRequest request,
        HttpContext context,
        RegimenInteriorDbContext db,
        CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";

        // Autorización explícita, sin bypass administrativo por pertenecer a otros módulos.
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.HasClaim(InstitutionalClaims.Scope, "order") ||
            !context.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior) ||
            string.IsNullOrWhiteSpace(context.User.FindFirst("sub")?.Value))
            return Results.Forbid();

        if (assemblyId == Guid.Empty || personId == Guid.Empty)
            return Results.BadRequest(new { code = "INVALID_ASSEMBLY_OR_PERSON_ID" });

        if (request is null ||
            request.Action is not (
                AssemblyReviewDecisionAdmissionPolicy.RequestCorrection or
                AssemblyReviewDecisionAdmissionPolicy.ReferValidation or
                AssemblyReviewDecisionAdmissionPolicy.ConfirmIneligible or
                AssemblyReviewDecisionAdmissionPolicy.AttendanceOnly) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000 ||
            string.IsNullOrWhiteSpace(request.EvidenceReference) ||
            request.EvidenceReference.Trim().Length > 500)
            return Results.BadRequest(new { code = "INVALID_REVIEW_DECISION" });

        // SERIALIZABLE más triggers draft/append-only: sin aceptación tardía al cierre.
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var assembly = await db.Assemblies.SingleOrDefaultAsync(
            x => x.Id == assemblyId, ct);
        if (assembly is null)
            return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });

        var member = await db.AssemblyMemberStatuses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssemblyId == assemblyId && x.PersonId == personId, ct);
        if (member is null)
            return Results.NotFound(new { code = "ASSEMBLY_MEMBER_NOT_FOUND" });

        var decision = AssemblyReviewDecisionAdmissionPolicy.Prepare(
            context.User, assembly, member, request.Action, request.Reason,
            request.EvidenceReference, DateTimeOffset.UtcNow);
        if (decision is null)
            return Results.Conflict(new { code = "ASSEMBLY_NOT_EDITABLE" });

        db.AssemblyReviewDecisionEvents.Add(decision);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg &&
            pg.SqlState is "23514" or "40001")
        {
            // Trigger draft o carrera de serialización: rollback por disposición de tx.
            return Results.Conflict(new { code = "ASSEMBLY_CHANGED_RETRY_REVIEW" });
        }

        // Usar la fábrica canónica para actor, correlación y saneamiento de metadatos.
        // SQL interpolado parametrizado en el contexto de RI evita una segunda conexión
        // sin transacción compartida: si falla auditoría no se persiste la decisión.
        var audit = AuditEventFactory.Create(
            context, "regimen.asamblea.decision.created",
            nameof(AssemblyReviewDecisionEventRecord), decision.Id.ToString("D"),
            member.OrganizationId, AuditResults.Success,
            new { decision.AssemblyId, decision.Action, DecisionEventId = decision.Id });

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
            decisionEventId = decision.Id,
            assemblyId,
            action = decision.Action,
            recordedAtUtc = decision.OccurredAtUtc,
            provisional = true,
            mayVoteConfirmed = false,
            officiallyClosed = false
        });
    }
}

public sealed record AssemblyReviewDecisionHttpRequest(
    string? Action,
    string? Reason,
    string? EvidenceReference);
