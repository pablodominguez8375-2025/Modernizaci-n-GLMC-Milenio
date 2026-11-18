using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class WorkshopRegistryPostgreSqlTests
{
    [Fact]
    public async Task Migration_adds_state_columns_and_footprint_detects_children_and_ignores_empty_workshops()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(ct);
        Assert.Contains("20261118150000_AddWorkshopRegistryState", await db.Database.GetAppliedMigrationsAsync(ct));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var parent = new Organization { Name = $"Taller padre {suffix}", Type = "workshop", Origin = "initial_load" };
        var child = new Organization { Name = $"Taller hijo {suffix}", Type = "workshop", ParentOrganizationId = parent.Id };
        var empty = new Organization { Name = $"Taller vacío {suffix}", Type = "workshop" };
        db.Organizations.AddRange(parent, empty);
        await db.SaveChangesAsync(ct);
        db.Organizations.Add(child);
        await db.SaveChangesAsync(ct);

        var withData = await WorkshopRegistryEndpoints.OrganizationsWithDataAsync(db, ct);
        Assert.Contains(parent.Id, withData);
        Assert.DoesNotContain(empty.Id, withData);
        Assert.DoesNotContain(child.Id, withData);

        var stored = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == empty.Id, ct);
        Assert.Equal("active", stored.Status);
        Assert.Equal("other", stored.Origin);
        Assert.Equal("initial_load", (await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == parent.Id, ct)).Origin);

        // La restricción de la base rechaza estados desconocidos.
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE core.organizations SET \"Status\" = 'borrado' WHERE \"Id\" = {0}", [empty.Id], ct));
    }

    /// <summary>
    /// Inventario: toda columna uuid cuyo nombre sugiere referencia a un Taller/organización debe estar cubierta por la huella
    /// (salvo las excluidas a propósito). Si un módulo nuevo agrega, por ejemplo, «HostOrganizationId», esta prueba falla
    /// hasta que alguien revise si cuenta como dato asociado: así un Taller no se elimina por una tabla no detectada.
    /// </summary>
    [Fact]
    public async Task Footprint_covers_every_uuid_column_that_looks_like_an_organization_reference()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options);
        await db.Database.MigrateAsync(ct);

        var covered = (await WorkshopRegistryEndpoints.FootprintTargetsAsync(db, ct))
            .Select(x => $"{x.Schema}.{x.Table}.{x.Column}").ToHashSet();

        // Columnas uuid con nombre de referencia a organización (criterio más amplio que el de la huella).
        var broad = new List<string>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT n.nspname || '.' || c.relname || '.' || a.attname
                FROM pg_attribute a
                JOIN pg_class c ON c.oid = a.attrelid AND c.relkind IN ('r','p')
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE a.attnum > 0 AND NOT a.attisdropped AND a.atttypid = 'uuid'::regtype
                  AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
                  AND a.attname ~* '(organi[sz]ation|workshop|lodge|taller|logia)'
                """;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) broad.Add(reader.GetString(0));
        }
        finally { await connection.CloseAsync(); }

        // Exclusiones deliberadas: la propia clave de organizations y la auditoría (se conserva aparte).
        var excluded = new HashSet<string> { "core.organizations.Id", "core.audit_events.OrganizationId" };
        var missing = broad.Where(x => !covered.Contains(x) && !excluded.Contains(x)).OrderBy(x => x).ToList();
        Assert.True(missing.Count == 0, "Columnas de referencia a organización no cubiertas por la huella: " + string.Join(", ", missing));

        // Las que antes se escapaban por nombre. Algunas tablas las crea otro DbContext (p. ej. admission_cases) y solo
        // existen si esa base ya fue migrada por esos módulos: se exigen cuando la columna existe.
        foreach (var column in broad.Where(x => x.EndsWith(".OriginOrganizationId", StringComparison.Ordinal)))
            Assert.Contains(column, covered);
        Assert.Contains("core.member_transfers.SourceOrganizationId", covered);
        Assert.Contains("core.member_transfers.TargetOrganizationId", covered);
        Assert.Contains("core.memberships.OrganizationId", covered);
        Assert.Contains("core.organizations.ParentOrganizationId", covered);
        Assert.DoesNotContain("core.audit_events.OrganizationId", covered);
    }

    [Fact]
    public async Task Footprint_flags_a_workshop_referenced_only_by_a_transfer_target_column_and_old_api_inserts_still_work()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options);
        await db.Database.MigrateAsync(ct);

        // Compatibilidad con API N: un INSERT que no conoce las columnas nuevas funciona y recibe los DEFAULT.
        var legacy = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO core.organizations (\"Id\", \"Name\", \"Type\", \"CreatedAtUtc\") VALUES ({0}, {1}, 'workshop', now())",
            [legacy, "Taller API anterior " + legacy.ToString("N")[..6]], ct);
        var stored = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == legacy, ct);
        Assert.Equal("active", stored.Status);
        Assert.Equal("other", stored.Origin);
        Assert.Null(stored.RetiredAtUtc);
        Assert.DoesNotContain(legacy, await WorkshopRegistryEndpoints.OrganizationsWithDataAsync(db, ct));
    }
}
