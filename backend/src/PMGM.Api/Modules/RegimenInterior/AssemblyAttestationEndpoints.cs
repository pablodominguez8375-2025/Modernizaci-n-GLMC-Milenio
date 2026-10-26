using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Constancias de Régimen Interior para el cierre del padrón (#533, entrega 2). Solo Régimen Interior
/// escribe; la Gran Secretaría lee. Cada constancia exige referencia al acta y declaración; es append-only
/// y auditada en la misma transacción. No concede sufragio ni cierra el padrón.
/// </summary>
public static class AssemblyAttestationEndpoints
{
    public static async Task<IResult> GetAsync(
        Guid assemblyId, HttpContext context, RegimenInteriorDbContext db, CancellationToken ct)
    {
        if (!AssemblyPreliminaryReviewEndpoints.CanRead(context.User)) return Results.Forbid();
        context.Response.Headers.CacheControl = "private, no-store";
        if (assemblyId == Guid.Empty) return Results.BadRequest(new { code = "INVALID_ASSEMBLY_ID" });
        if (!await db.Assemblies.AsNoTracking().AnyAsync(a => a.Id == assemblyId, ct))
            return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });
        var events = await db.AssemblyAttestationEvents.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);
        var current = AssemblyAttestationPolicy.Current(events);
        return Results.Ok(new
        {
            assemblyId,
            items = AssemblyAttestationPolicy.Kinds.Select(k => new
            {
                kind = k,
                title = AssemblyAttestationPolicy.Title(k),
                attested = current.ContainsKey(k),
                actaReference = current.GetValueOrDefault(k)?.ActaReference,
                statement = current.GetValueOrDefault(k)?.Statement,
                recordedAtUtc = current.GetValueOrDefault(k)?.OccurredAtUtc
            })
        });
    }

    public static async Task<IResult> PostAsync(
        Guid assemblyId, AssemblyAttestationRequest request,
        HttpContext context, RegimenInteriorDbContext db, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.HasClaim(InstitutionalClaims.Scope, "order") ||
            !context.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior) ||
            string.IsNullOrWhiteSpace(context.User.FindFirst("sub")?.Value))
            return Results.Forbid();
        if (assemblyId == Guid.Empty) return Results.BadRequest(new { code = "INVALID_ASSEMBLY_ID" });
        var invalid = AssemblyAttestationPolicy.Validate(request?.Kind, request?.Action, request?.ActaReference, request?.Statement);
        if (invalid is not null) return Results.BadRequest(new { code = invalid });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var assembly = await db.Assemblies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assemblyId, ct);
        if (assembly is null) return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });
        if (assembly.Status != "draft" || assembly.ClosedAtUtc is not null)
            return Results.Conflict(new { code = "ASSEMBLY_NOT_EDITABLE" });

        var events = await db.AssemblyAttestationEvents.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);
        var current = AssemblyAttestationPolicy.Current(events);
        Guid? reverses = null;
        if (request!.Action == AssemblyAttestationPolicy.Attest)
        {
            if (current.ContainsKey(request.Kind!)) return Results.Conflict(new { code = "ATTESTATION_ALREADY_ACTIVE" });
            // Sin asambleístas acreditados no hay nada que certificar (no se certifica en el vacío).
            if (!await db.AssemblyMemberStatuses.AsNoTracking().AnyAsync(x => x.AssemblyId == assemblyId, ct))
                return Results.Conflict(new { code = "NO_ACCREDITED_MEMBERS" });
        }
        else
        {
            if (!current.TryGetValue(request.Kind!, out var active))
                return Results.Conflict(new { code = "NO_ACTIVE_ATTESTATION_TO_REVOKE" });
            reverses = active.Id;
        }

        var record = new AssemblyAttestationEventRecord
        {
            AssemblyId = assemblyId, Kind = request.Kind!, Action = request.Action!,
            ActaReference = request.ActaReference!.Trim(), Statement = request.Statement!.Trim(),
            RecordedBySubject = context.User.FindFirst("sub")!.Value,
            CutoffDate = assembly.CutoffDate, ReversesEventId = reverses
        };
        db.AssemblyAttestationEvents.Add(record);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState is "23505" or "23514" or "40001")
        {
            return Results.Conflict(new { code = "ASSEMBLY_CHANGED_RETRY_REVIEW" });
        }

        var audit = AuditEventFactory.Create(
            context, "regimen.asamblea.constancia." + record.Action,
            nameof(AssemblyAttestationEventRecord), record.Id.ToString("D"),
            null, AuditResults.Success, new { record.AssemblyId, record.Kind, record.Action, AttestationEventId = record.Id });
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
            attestationEventId = record.Id, assemblyId, kind = record.Kind, action = record.Action,
            recordedAtUtc = record.OccurredAtUtc, provisional = true, mayVoteConfirmed = false, officiallyClosed = false
        });
    }
}

public sealed record AssemblyAttestationRequest(string? Kind, string? Action, string? ActaReference, string? Statement);
