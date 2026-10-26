using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Audit;

/// <summary>
/// Read-only PostgreSQL metadata required by the three concurrent requests
/// on the Talleres (supervision) page. No user-controlled SQL or row reads.
/// This is a diagnostic, never an authorization or migration mechanism.
/// </summary>
public static class SupportLodgeDataHealthEndpoints
{
    private sealed record RelationCheck(
        string Resource, string Relation, string[] RequiredColumns);

    private static readonly RelationCheck[] Checks =
    [
        new("tenidas", "core.lodge_meetings",
            ["Id", "OrganizationId", "MeetingDate", "MeetingType", "Grade", "Status",
             "Title", "CreatedAtUtc", "HeldAtUtc", "ClosedAtUtc", "Modality", "CeremonyType",
             "LocationReference", "VirtualAccessReference"]),
        new("instrucciones", "core.lodge_instruction_sessions",
            ["Id", "OrganizationId", "InstructionDate", "Grade", "Topic",
             "ResponsibleOffice", "InstructorMemberId", "Status", "CreatedAtUtc", "CreatedBySubject"]),
        new("miembros", "core.memberships",
            ["Id", "OrganizationId", "MemberId", "EndDate", "Status"]),
        new("miembros", "core.members",
            ["Id", "PersonId"]),
        new("miembros", "core.people",
            ["Id", "FirstNames", "LastNames"])
    ];

    // Relation and required column names are bound parameters, not SQL fragments.
    // CASE avoids throwing on missing tables in has_table_privilege.
    private const string MetadataSql = """
        SELECT
          to_regclass(@relation) IS NOT NULL,
          CASE WHEN to_regnamespace('core') IS NULL THEN false
               ELSE has_schema_privilege(current_user, 'core', 'USAGE') END,
          CASE WHEN to_regclass(@relation) IS NULL THEN false
               ELSE has_table_privilege(current_user, @relation, 'SELECT') END,
          (SELECT COUNT(*) = cardinality(@columns)
           FROM pg_catalog.pg_attribute AS a
           WHERE a.attrelid = to_regclass(@relation)
             AND a.attnum > 0 AND NOT a.attisdropped
             AND a.attname = ANY(@columns))
        """;

    public static IEndpointRouteBuilder MapSupportLodgeDataHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/support/lodge-data-health", GetAsync)
            .RequireAuthorization()
            .WithTags("Sistema — Soporte técnico");
        endpoints.MapGet("/api/system/support/lodge-query-probe/{organizationId:guid}", ProbeAsync)
            .RequireAuthorization()
            .WithTags("Sistema — Soporte técnico");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        IInstitutionalAccessService access,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        // The permission check MUST happen before even opening a connection.
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();

        context.Response.Headers.CacheControl = "private, no-store";
        var results = new List<SupportLodgeRelationStatus>(Checks.Length);
        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var connection = db.Database.GetDbConnection();
                foreach (var check in Checks)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = MetadataSql;
                    command.CommandTimeout = 5;
                    command.Parameters.Add(new NpgsqlParameter("relation", NpgsqlDbType.Text)
                    {
                        Value = check.Relation
                    });
                    command.Parameters.Add(new NpgsqlParameter("columns",
                        NpgsqlDbType.Array | NpgsqlDbType.Text)
                    {
                        Value = check.RequiredColumns
                    });
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken))
                        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                    results.Add(new SupportLodgeRelationStatus(
                        check.Resource, check.Relation,
                        reader.GetBoolean(0), reader.GetBoolean(1),
                        reader.GetBoolean(2), reader.GetBoolean(3)));
                }
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
        catch (DbException)
        {
            // Do not send SQLSTATE, SQL fragments, role, host or stack trace.
            return Results.Problem(
                title: "No se pudo verificar el estado de la base QA.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TimeoutException)
        {
            return Results.Problem(
                title: "Tiempo de verificación QA agotado.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            status = results.All(x => x.Present && x.SchemaUsable
                && x.SelectGranted && x.RequiredColumnsPresent) ? "ready" : "attention",
            mode = "read_only_catalog",
            scope = "gestion_logial_taller",
            note = "Solo metadatos del esquema y permisos del rol API; no garantiza que las consultas reales respondan 200.",
            checkedAtUtc = DateTimeOffset.UtcNow,
            items = results
        });
    }

    // Support-only, read-only replay of the three DB query shapes used by
    // Talleres (supervisión). The result NEVER includes records, names,
    // identifiers, exception text, SQLSTATE, claims, or SQL.
    private static async Task<IResult> ProbeAsync(
        Guid organizationId,
        HttpContext context,
        IInstitutionalAccessService access,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext lodgeDb,
        CancellationToken cancellationToken)
    {
        // Check authorization BEFORE any database access; the caller must also
        // be authenticated by RequireAuthorization.
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        context.Response.Headers.CacheControl = "private, no-store";

        bool isWorkshop;
        try
        {
            isWorkshop = await institutionalDb.Organizations.AsNoTracking()
                .AnyAsync(x => x.Id == organizationId && x.Type == "workshop",
                    cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!isWorkshop) return Results.NotFound();

        var checks = new[]
        {
            await ReadProbeAsync("tenidas", async ct =>
            {
                var rows = await lodgeDb.LodgeMeetings.AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId)
                    .OrderByDescending(x => x.MeetingDate)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .Take(250).ToListAsync(ct);
                _ = rows.Count;
            }, cancellationToken),
            await ReadProbeAsync("miembros", async ct =>
            {
                // Identical materialized columns, JOIN and ordering to
                // GetMemberOptionsAsync; values remain exclusively in memory.
                var rows = await institutionalDb.Memberships.AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId &&
                                x.EndDate == null &&
                                x.Status == MembershipCodes.MembershipStatus.Active)
                    .OrderBy(x => x.Member.Person.LastNames)
                    .ThenBy(x => x.Member.Person.FirstNames)
                    .Select(x => new
                    {
                        x.MemberId,
                        Name = (x.Member.Person.FirstNames + " " +
                                x.Member.Person.LastNames).Trim()
                    })
                    .Take(1000).ToListAsync(ct);
                _ = rows.Count;
            }, cancellationToken),
            await ReadProbeAsync("instrucciones", async ct =>
            {
                var rows = await lodgeDb.LodgeInstructionSessions.AsNoTracking()
                    .Where(x => x.OrganizationId == organizationId)
                    .OrderByDescending(x => x.InstructionDate)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .Take(250)
                    .Select(x => new
                    {
                        x.Id, x.OrganizationId, x.InstructionDate, x.Grade,
                        x.Topic, x.ResponsibleOffice, x.InstructorMemberId,
                        x.Status
                    }).ToListAsync(ct);
                _ = rows.Count;
            }, cancellationToken)
        };

        return Results.Ok(new
        {
            status = checks.All(x => x.Result == "ok") ? "ready" : "attention",
            mode = "read_only_queries",
            scope = "gestion_logial_taller",
            note = "Solo resultado de tres lecturas aisladas; no diagnostica por sí solo permisos del usuario ni causa raíz.",
            items = checks
        });
    }

    private static async Task<SupportLodgeProbeStatus> ReadProbeAsync(
        string resource, Func<CancellationToken, Task> read, CancellationToken ct)
    {
        try
        {
            await read(ct);
            return new SupportLodgeProbeStatus(resource, "ok");
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Failed query gets a fixed status; no sensitive exception details.
            // The next step is the protected, correlated QA host log.
            return new SupportLodgeProbeStatus(resource, "failed");
        }
    }

    public sealed record SupportLodgeProbeStatus(string Resource, string Result);

    public sealed record SupportLodgeRelationStatus(
        string Resource,
        string Relation,
        bool Present,
        bool SchemaUsable,
        bool SelectGranted,
        bool RequiredColumnsPresent);
}
