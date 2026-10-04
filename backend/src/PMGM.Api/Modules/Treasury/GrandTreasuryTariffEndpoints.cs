using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class GrandTreasuryTariffEndpoints
{
    public static IEndpointRouteBuilder MapGrandTreasuryTariffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tesoreria/tarifarios/decretos").RequireAuthorization().WithTags("Gran Tesorería");
        group.MapGet("", async (HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct) =>
        {
            if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
            var items = await GrandTreasuryTariff.LoadAsync(db, ct);
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { version = items.Select(x => x.Version).DefaultIfEmpty().Max(), total = items.Count, items });
        });
        group.MapPost("", RegisterAsync);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(RegisterTariffRequest request, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var error = GrandTreasuryTariff.Validate(request, GrandTreasuryTariff.Today());
        if (error is not null) return Results.BadRequest(new { message = error });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var current = await db.GrandTreasuryTariffVersions.MaxAsync(x => (int?)x.Version, ct) ?? 0;
            if (current != request.ExpectedVersion)
                return Results.Conflict(new { message = "El tarifario cambió. Actualice la lista antes de registrar otra versión." });
            var row = new GrandTreasuryTariffVersion
            {
                Version = current + 1, Number = request.Number.Trim(), DecreeDate = request.DecreeDate,
                EffectiveFrom = request.EffectiveFrom, EffectiveUntil = request.EffectiveUntil,
                SourceReference = request.SourceReference.Trim(), Status = request.Status,
                Payload = JsonSerializer.Serialize(new TariffValues(request.Rates, request.CeremonyRights, request.Unemployment), GrandTreasuryTariff.Json)
            };
            db.GrandTreasuryTariffVersions.Add(row);
            audit.Add(context, "treasury.tariff.version_registered", nameof(GrandTreasuryTariffVersion), row.Id.ToString(),
                null, AuditResults.Success, new { row.Version, row.Number, row.Status, row.DecreeDate, row.EffectiveFrom, row.EffectiveUntil });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/tesoreria/tarifarios/decretos/{row.Id}", GrandTreasuryTariff.ToDto(row));
        }
        catch (Exception ex) when (IsConcurrency(ex))
        {
            await transaction.RollbackAsync(ct);
            return Results.Conflict(new { message = "Otra sesión registró un decreto. Actualice y confirme nuevamente." });
        }
    }
    private static bool IsConcurrency(Exception ex)
        => ex is PostgresException { SqlState: "40001" or "40P01" or "23505" } ||
            (ex.InnerException is not null && IsConcurrency(ex.InnerException));
}
