using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.SystemConfiguration;

public sealed record WorkshopRegistryRequest(string? Reason, string? Confirmation);

/// <summary>
/// Sistema › Talleres (#1035). Ver y descargar: Máster y superusuarios. Eliminar, dar de baja y reactivar: solo Máster.
/// «Tiene datos asociados» se calcula sobre el catálogo de PostgreSQL (toda tabla con una columna uuid que apunte a
/// core.organizations, por clave foránea o por nombre OrganizationId/ParentOrganizationId) y solo se expone como booleano (Ley 21.719).
/// No se cuentan los eventos de auditoría: la auditoría se conserva aparte y no impide eliminar un Taller vacío.
/// </summary>
public static class WorkshopRegistryEndpoints
{
    private const string FootprintColumnsSql = """
        SELECT n.nspname, c.relname, a.attname
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid AND c.relkind IN ('r','p')
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE a.attnum > 0 AND NOT a.attisdropped
          AND a.atttypid = 'uuid'::regtype
          AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
          AND NOT (n.nspname = 'core' AND c.relname = 'organizations' AND a.attname = 'Id')
          AND NOT (c.relname = 'audit_events')
          AND NOT (c.relname LIKE '\_\_EFMigrations%')
          AND (a.attname IN ('OrganizationId','ParentOrganizationId')
               OR EXISTS (SELECT 1 FROM pg_constraint k
                          WHERE k.contype = 'f' AND k.conrelid = c.oid
                            AND k.confrelid = 'core.organizations'::regclass AND a.attnum = ANY (k.conkey)))
        ORDER BY 1, 2, 3
        """;

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    public static IEndpointRouteBuilder MapWorkshopRegistryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system/workshops").RequireAuthorization().WithTags("Registro de Talleres");
        group.MapGet("", GetRegistry);
        group.MapGet("/export.csv", ExportCsv);
        group.MapPost("/{id:guid}/{action}", Run);
        return endpoints;
    }

    /// <summary>Ids de organización que tienen algún registro asociado. Solo lectura.</summary>
    public static async Task<HashSet<Guid>> OrganizationsWithDataAsync(PmgmDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            var targets = new List<(string Schema, string Table, string Column)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = FootprintColumnsSql;
                if (db.Database.CurrentTransaction is { } tx) command.Transaction = tx.GetDbTransaction();
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) targets.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }

            var found = new HashSet<Guid>();
            foreach (var (schema, table, column) in targets)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT DISTINCT {Quote(column)} FROM {Quote(schema)}.{Quote(table)} WHERE {Quote(column)} IS NOT NULL";
                if (db.Database.CurrentTransaction is { } tx) command.Transaction = tx.GetDbTransaction();
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) found.Add(reader.GetGuid(0));
            }
            return found;
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    private sealed record Row(Guid Id, string? Number, string Name, string Type, string? OrienteCode, string? City,
        DateTimeOffset CreatedAtUtc, string Origin, string Status, DateTimeOffset? RetiredAtUtc);

    private static async Task<List<Row>> LoadAsync(PmgmDbContext db, CancellationToken ct)
    {
        var rows = await db.Organizations.AsNoTracking()
            .Select(x => new Row(x.Id, x.Number, x.Name, x.Type, x.OrienteCode, x.City, x.CreatedAtUtc, x.Origin, x.Status, x.RetiredAtUtc))
            .ToListAsync(ct);
        return rows
            .OrderBy(x => x.Type == WorkshopRegistryCodes.GrandLodgeType ? 0 : x.Type == WorkshopRegistryCodes.SupportType ? 1 : 2)
            .ThenBy(x => int.TryParse(x.Number, out var n) ? n : int.MaxValue)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string ShortCode(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private static async Task<IResult> GetRegistry(HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!access.IsPlatformOperator(ctx.User)) return Results.Forbid();
        var rows = await LoadAsync(db, ct);
        var withData = await OrganizationsWithDataAsync(db, ct);
        var items = rows.Select(x => new
        {
            id = x.Id,
            shortCode = ShortCode(x.Id),
            number = x.Number,
            name = x.Name,
            type = x.Type,
            orienteCode = x.OrienteCode,
            city = x.City,
            createdAtUtc = x.CreatedAtUtc,
            origin = WorkshopRegistryCodes.NormalizeOrigin(x.Origin),
            status = x.Status,
            retiredAtUtc = x.RetiredAtUtc,
            hasData = withData.Contains(x.Id),
            @protected = new WorkshopRegistryFacts(x.Id, x.Number, x.Type, x.Status, false).Protected
        }).ToArray();
        return Results.Ok(new
        {
            items,
            canManage = access.IsPlatformSuperAdmin(ctx.User),
            reasonMinLength = WorkshopRegistryCodes.ReasonMinLength,
            reasonMaxLength = WorkshopRegistryCodes.ReasonMaxLength
        });
    }

    private static async Task<IResult> ExportCsv(HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!access.IsPlatformOperator(ctx.User)) return Results.Forbid();
        var rows = await LoadAsync(db, ct);
        var withData = await OrganizationsWithDataAsync(db, ct);
        var bytes = WorkshopRegistryCsv.Build(rows.Select(x => new WorkshopCsvRow(x.Id, ShortCode(x.Id), x.Number, x.Name, x.Type,
            x.OrienteCode, x.City, WorkshopRegistryCodes.NormalizeOrigin(x.Origin), x.Status, withData.Contains(x.Id), x.CreatedAtUtc)));
        audit.Add(ctx, "system.workshops.exported", "WorkshopRegistry", "all", null, AuditResults.Success, new { rows = rows.Count });
        await db.SaveChangesAsync(ct);
        return Results.File(bytes, "text/csv; charset=utf-8", "talleres-registrados.csv");
    }

    private static async Task<IResult> Run(Guid id, string action, WorkshopRegistryRequest? request, HttpContext ctx,
        PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!access.IsPlatformOperator(ctx.User)) return Results.Forbid();
        if (!WorkshopRegistryActions.IsKnown(action)) return Results.NotFound();
        var isMaster = access.IsPlatformSuperAdmin(ctx.User);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var organization = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (organization is null) return Results.NotFound();
        var hasData = (await OrganizationsWithDataAsync(db, ct)).Contains(id);
        var facts = new WorkshopRegistryFacts(id, organization.Number, organization.Type, organization.Status, hasData);
        var decision = WorkshopRegistryPolicy.Decide(action, isMaster, facts, request?.Reason, request?.Confirmation);

        if (decision != WorkshopRegistryDecisionCode.Allowed)
        {
            audit.Add(ctx, "system.workshops." + action + "_rejected", "Organization", id.ToString(), id, AuditResults.Rejected,
                new { action, code = decision.ToString() });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return decision switch
            {
                WorkshopRegistryDecisionCode.NotMaster or WorkshopRegistryDecisionCode.Protected => Results.Forbid(),
                WorkshopRegistryDecisionCode.InvalidReason or WorkshopRegistryDecisionCode.ConfirmationMismatch =>
                    Results.BadRequest(new { code = decision.ToString() }),
                _ => Results.Conflict(new { code = decision.ToString() })
            };
        }

        var reason = request!.Reason!.Trim();
        try
        {
            switch (action)
            {
                case WorkshopRegistryActions.Delete:
                    db.Organizations.Remove(organization);
                    break;
                case WorkshopRegistryActions.Retire:
                    organization.Status = WorkshopRegistryCodes.Retired;
                    organization.RetiredAtUtc = DateTimeOffset.UtcNow;
                    organization.RetiredReason = reason;
                    break;
                default:
                    organization.Status = WorkshopRegistryCodes.Active;
                    organization.RetiredAtUtc = null;
                    organization.RetiredReason = null;
                    break;
            }
            audit.Add(ctx, "system.workshops." + action, "Organization", id.ToString(), action == WorkshopRegistryActions.Delete ? null : id,
                AuditResults.Success, new { action, shortCode = ShortCode(id), number = organization.Number, type = organization.Type, reason });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Una clave foránea apareció entre la lectura y la escritura: el Taller tiene datos.
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            audit.Add(ctx, "system.workshops." + action + "_rejected", "Organization", id.ToString(), id, AuditResults.Rejected,
                new { action, code = nameof(WorkshopRegistryDecisionCode.HasData) });
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Conflict(new { code = nameof(WorkshopRegistryDecisionCode.HasData) });
        }
        return Results.Ok(new { id, action, completed = true });
    }
}
