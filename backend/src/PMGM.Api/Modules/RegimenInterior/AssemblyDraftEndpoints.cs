using System.Data;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Crear y listar Asambleas preliminares. NO calcula electores ni concede voto.
/// </summary>
public static class AssemblyDraftEndpoints
{
    public static async Task<IResult> ListAsync(
        int? pageSize, HttpContext ctx, RegimenInteriorDbContext db,
        PmgmDbContext appDb, IAuditService audit, CancellationToken ct)
    {
        if (!AssemblyPreliminaryReviewEndpoints.CanRead(ctx.User))
            return Results.Forbid();
        if (pageSize is < 1 or > 100)
            return Results.BadRequest(new { code = "INVALID_PAGE_SIZE" });
        ctx.Response.Headers.CacheControl = "private, no-store";
        var rows = await db.Assemblies.AsNoTracking()
            .OrderByDescending(a => a.MeetingDate)
            .ThenByDescending(a => a.CreatedAtUtc)
            .Take(pageSize ?? 50)
            .Select(a => new
            {
                a.Id, a.Name, a.MeetingDate, a.CutoffDate, a.Status,
                provisional = a.Status == "draft",
                officiallyClosed = false,
                mayVoteConfirmed = false
            })
            .ToArrayAsync(ct);
        audit.Add(ctx, "regimen.asamblea.list.consulted", nameof(AssemblyRecord),
            "list", null, AuditResults.Success, new { count = rows.Length });
        await appDb.SaveChangesAsync(ct);
        return Results.Ok(new { items = rows, pageSize = pageSize ?? 50 });
    }

    public static async Task<IResult> CreateAsync(
        AssemblyDraftCreateRequest request, HttpContext ctx,
        RegimenInteriorDbContext db, CancellationToken ct)
    {
        if (!AssemblyPreliminaryReviewEndpoints.CanRead(ctx.User))
            return Results.Forbid();

        ctx.Response.Headers.CacheControl = "private, no-store";
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 ||
            request.MeetingDate == default || request.CutoffDate == default ||
            request.CutoffDate > request.MeetingDate)
            return Results.BadRequest(new { code = "INVALID_ASSEMBLY_DATES_OR_NAME" });

        var assembly = new AssemblyRecord
        {
            Name = name,
            MeetingDate = request.MeetingDate,
            CutoffDate = request.CutoffDate,
            CreatedBySubject = ctx.User.FindFirst("sub")!.Value
        };
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        db.Assemblies.Add(assembly);
        await db.SaveChangesAsync(ct);

        var audit = AuditEventFactory.Create(ctx, "regimen.asamblea.draft.created",
            nameof(AssemblyRecord), assembly.Id.ToString("D"), null,
            AuditResults.Success, new
            {
                assembly.MeetingDate, assembly.CutoffDate,
                status = assembly.Status
            });
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
        return Results.Created(
            $"/api/regimen-interior/asambleas/{assembly.Id}/revision",
            new
            {
                assembly.Id, assembly.Name, assembly.MeetingDate,
                assembly.CutoffDate, assembly.Status,
                provisional = true,
                mayVoteConfirmed = false,
                officiallyClosed = false
            });
    }
}

public sealed record AssemblyDraftCreateRequest(
    string? Name,
    DateOnly MeetingDate,
    DateOnly CutoffDate);
