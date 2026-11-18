using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #1039 T1 contra PostgreSQL real, en una base aislada migrada solo con PmgmDbContext: inventario completo cubierto,
/// regla de solo lectura (INSERT/UPDATE/DELETE => PM409), organizations excluida salvo ParentOrganizationId,
/// serializacion con retire en dos conexiones y reactivacion.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class WorkshopRetiredGuardPostgreSqlTests
{
    private sealed record Target(string Schema, string Table, string Column);

    private static string? ConnectionString => Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");

    private static async Task<(string Name, string Connection, NpgsqlConnection Admin)> CreateDatabaseAsync(CancellationToken ct)
    {
        var name = "qa525_guard_" + Guid.NewGuid().ToString("N");
        var adminBuilder = new NpgsqlConnectionStringBuilder(ConnectionString!) { Database = "postgres", Pooling = false };
        var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync(ct);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync(ct);
        var cs = new NpgsqlConnectionStringBuilder(ConnectionString!) { Database = name, Pooling = false }.ConnectionString;
        await using var db = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(cs).Options);
        await db.Database.MigrateAsync(ct);
        return (name, cs, admin);
    }

    private static async Task DropAsync(string name, NpgsqlConnection admin)
    {
        await using (var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin)) await drop.ExecuteNonQueryAsync(CancellationToken.None);
        await admin.DisposeAsync();
    }

    private static async Task<NpgsqlConnection> OpenAsync(string cs, CancellationToken ct)
    {
        var c = new NpgsqlConnection(cs);
        await c.OpenAsync(ct);
        return c;
    }

    private static async Task<int> ExecAsync(NpgsqlConnection c, string sql, CancellationToken ct, NpgsqlTransaction? tx = null)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection c, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return (T)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<List<Target>> InventoryAsync(NpgsqlConnection c, CancellationToken ct)
    {
        var list = new List<Target>();
        await using var cmd = new NpgsqlCommand("""
            SELECT n.nspname, c.relname, a.attname
            FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid AND c.relkind = 'r'
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE a.attnum > 0 AND NOT a.attisdropped AND a.atttypid = 'uuid'::regtype
              AND n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
              AND NOT (n.nspname = 'core' AND c.relname = 'organizations' AND a.attname = 'Id')
              AND c.relname <> 'audit_events' AND c.relname NOT LIKE '\_\_EFMigrations%'
              AND (a.attname ~* '(organi[sz]ation|workshop|lodge|taller|logia)'
                   OR EXISTS (SELECT 1 FROM pg_constraint k WHERE k.contype='f' AND k.conrelid=c.oid
                              AND k.confrelid='core.organizations'::regclass AND a.attnum = ANY (k.conkey)))
            ORDER BY 1,2,3
            """, c);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new Target(r.GetString(0), r.GetString(1), r.GetString(2)));
        return list;
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static async Task<Guid> NewOrgAsync(NpgsqlConnection c, string status, CancellationToken ct, Guid? parent = null)
    {
        var id = Guid.NewGuid();
        await ExecAsync(c, $"INSERT INTO core.organizations (\"Id\",\"Name\",\"Type\",\"Status\",\"ParentOrganizationId\",\"CreatedAtUtc\") VALUES ('{id}','T {id:N}','workshop','{status}',{(parent is null ? "NULL" : "'" + parent + "'")},now())", ct);
        return id;
    }

    private static async Task<string?> StateOfAsync(Func<Task> action)
    {
        try { await action(); return null; }
        catch (PostgresException ex) { return ex.SqlState; }
    }

    /// <summary>INSERT con SOLO la columna de organizacion: el trigger BEFORE corre antes de NOT NULL/CHECK/FK.</summary>
    private static Task<string?> TryInsertAsync(NpgsqlConnection c, Target t, Guid org, CancellationToken ct)
        => StateOfAsync(() => ExecAsync(c, $"INSERT INTO {Q(t.Schema)}.{Q(t.Table)} ({Q(t.Column)}) VALUES ('{org}')", ct));


    /// <summary>
    /// INSERT valido generico: da valor tipado a toda columna NOT NULL sin DEFAULT. Con session_replication_role=replica se
    /// omiten FK y triggers (solo fixture); las restricciones CHECK siguen vigentes: la tabla que no admita el valor generico
    /// se descarta y el test exige un minimo de tablas sembradas.
    /// </summary>
    private static async Task<string> BuildInsertAsync(NpgsqlConnection c, Target t, Guid org, CancellationToken ct)
    {
        var columns = new List<(string Name, string Udt, bool Required)>();
        await using (var cmd = new NpgsqlCommand("SELECT column_name, udt_name, (is_nullable='NO' AND column_default IS NULL AND is_generated='NEVER' AND is_identity='NO') FROM information_schema.columns WHERE table_schema=@s AND table_name=@t", c))
        {
            cmd.Parameters.AddWithValue("s", t.Schema);
            cmd.Parameters.AddWithValue("t", t.Table);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) columns.Add((r.GetString(0), r.GetString(1), r.GetBoolean(2)));
        }
        var names = new List<string>();
        var values = new List<string>();
        foreach (var (name, udt, required) in columns)
        {
            if (name == t.Column) { names.Add(Q(name)); values.Add($"'{org}'"); continue; }
            if (!required) continue;
            names.Add(Q(name));
            values.Add(udt switch
            {
                "uuid" => "gen_random_uuid()",
                "text" or "varchar" or "bpchar" => "'x'",
                "timestamptz" or "timestamp" => "now()",
                "date" => "current_date",
                "bool" => "false",
                "int2" or "int4" or "int8" or "numeric" or "float4" or "float8" => "0",
                "jsonb" or "json" => "'{}'",
                "bytea" => "'\\x00'",
                _ => "NULL",
            });
        }
        return $"INSERT INTO {Q(t.Schema)}.{Q(t.Table)} ({string.Join(",", names)}) VALUES ({string.Join(",", values)})";
    }

    private static async Task<bool> TrySeedRowAsync(NpgsqlConnection c, Target t, Guid org, CancellationToken ct)
    {
        var sql = await BuildInsertAsync(c, t, org, ct);
        await ExecAsync(c, "SET session_replication_role = replica", ct);
        try { return await StateOfAsync(() => ExecAsync(c, sql, ct)) is null; }
        finally { await ExecAsync(c, "SET session_replication_role = DEFAULT", ct); }
    }

    /// <summary>Mismo INSERT valido dentro de una transaccion abierta; la guardia se fuerza con ENABLE ALWAYS (replica omite FK).</summary>
    private static async Task SeedInTxAsync(NpgsqlConnection c, Target t, Guid org, NpgsqlTransaction tx, CancellationToken ct)
    {
        var sql = await BuildInsertAsync(c, t, org, ct);
        await ExecAsync(c, "SET LOCAL session_replication_role = replica", ct, tx);
        await ExecAsync(c, sql, ct, tx);
    }

    [Fact]
    public async Task Every_inventory_column_has_the_guard_trigger_with_that_column()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var (name, cs, admin) = await CreateDatabaseAsync(ct);
        try
        {
            await using var c = await OpenAsync(cs, ct);
            var inventory = await InventoryAsync(c, ct);
            Assert.True(inventory.Count >= 40, "inventario inesperadamente pequeño: " + inventory.Count);
            var installed = new HashSet<string>();
            await using (var cmd = new NpgsqlCommand("""
                SELECT n.nspname||'.'||c.relname||'.'||a
                FROM pg_trigger g JOIN pg_class c ON c.oid=g.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace,
                LATERAL unnest(string_to_array(encode(g.tgargs,'escape'), E'\\000')) a
                WHERE g.tgname='a00_workshop_retired_guard' AND NOT g.tgisinternal AND a <> ''
                """, c))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) installed.Add(r.GetString(0));
            var missing = inventory.Where(t => !installed.Contains($"{t.Schema}.{t.Table}.{t.Column}")).Select(t => $"{t.Schema}.{t.Table}.{t.Column}").ToList();
            Assert.True(missing.Count == 0, "Columnas de organizacion sin guardia (ejecutar core.install_workshop_retired_guard()): " + string.Join(", ", missing));
        }
        finally { await DropAsync(name, admin); }
    }

    [Fact]
    public async Task Retired_workshop_is_read_only_everywhere_active_one_is_not_blocked_and_reactivation_restores_it()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var (name, cs, admin) = await CreateDatabaseAsync(ct);
        try
        {
            await using var c = await OpenAsync(cs, ct);
            var inventory = (await InventoryAsync(c, ct)).Where(t => !(t.Table == "organizations")).ToList();
            var active = await NewOrgAsync(c, "active", ct);
            var retired = await NewOrgAsync(c, "retired", ct);

            var blocked = new List<string>();
            foreach (var t in inventory)
            {
                Assert.Equal("PM409", await TryInsertAsync(c, t, retired, ct) ?? $"no-error {t.Table}.{t.Column}");
                // el Taller activo NO recibe PM409 (puede fallar por NOT NULL/CHECK de otras columnas: es otro SQLSTATE)
                Assert.NotEqual("PM409", await TryInsertAsync(c, t, active, ct));
                blocked.Add(t.Table);
            }

            // UPDATE/DELETE: filas reales sembradas con un INSERT generico valido (ver TrySeedRowAsync).
            var seeded = new List<Target>();
            foreach (var t in inventory)
                if (await TrySeedRowAsync(c, t, active, ct)) seeded.Add(t);
            Assert.True(seeded.Count >= 3, "se necesitan al menos 3 tablas con fila sembrable para probar UPDATE/DELETE; hay " + seeded.Count);

            await ExecAsync(c, $"UPDATE core.organizations SET \"Status\"='retired', \"RetiredAtUtc\"=now(), \"RetiredReason\"='prueba de baja 1039' WHERE \"Id\"='{active}'", ct);
            foreach (var t in seeded)
            {
                Assert.Equal("PM409", await StateOfAsync(() => ExecAsync(c, $"UPDATE {Q(t.Schema)}.{Q(t.Table)} SET {Q(t.Column)} = {Q(t.Column)} WHERE {Q(t.Column)}='{active}'", ct)));
                Assert.Equal("PM409", await StateOfAsync(() => ExecAsync(c, $"DELETE FROM {Q(t.Schema)}.{Q(t.Table)} WHERE {Q(t.Column)}='{active}'", ct)));
                Assert.True(await ScalarAsync<long>(c, $"SELECT count(*) FROM {Q(t.Schema)}.{Q(t.Table)} WHERE {Q(t.Column)}='{active}'", ct) >= 1, "la lectura historica sigue visible");
            }

            // la propia fila de organizations (retire/reactivate) NO se bloquea; reactivar libera todo
            await ExecAsync(c, $"UPDATE core.organizations SET \"Status\"='active', \"RetiredAtUtc\"=NULL, \"RetiredReason\"=NULL WHERE \"Id\"='{active}'", ct);
            foreach (var t in seeded)   // algunas tablas ya son append-only por otros triggers (P0001): solo importa que no sea PM409
                Assert.NotEqual("PM409", await StateOfAsync(() => ExecAsync(c, $"UPDATE {Q(t.Schema)}.{Q(t.Table)} SET {Q(t.Column)} = {Q(t.Column)} WHERE {Q(t.Column)}='{active}'", ct)));

            // un Taller hijo no puede colgar de un padre dado de baja; mover un hijo existente a otro padre activo si
            Assert.Equal("PM409", await StateOfAsync(() => NewOrgAsync(c, "active", ct, retired)));
            var child = await NewOrgAsync(c, "active", ct, active);
            await ExecAsync(c, $"UPDATE core.organizations SET \"Name\"='renombrado' WHERE \"Id\"='{retired}'", ct);
            Assert.Equal("PM409", await StateOfAsync(() => ExecAsync(c, $"UPDATE core.organizations SET \"ParentOrganizationId\"='{retired}' WHERE \"Id\"='{child}'", ct)));
        }
        finally { await DropAsync(name, admin); }
    }

    [Fact]
    public async Task Retire_and_insert_serialize_in_two_connections_in_both_orders()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var (name, cs, admin) = await CreateDatabaseAsync(ct);
        try
        {
            await using var setup = await OpenAsync(cs, ct);
            Target? target = null;
            foreach (var candidate in (await InventoryAsync(setup, ct)).Where(t => t.Table != "organizations"))
            {
                var probe = await NewOrgAsync(setup, "active", ct);
                if (await TrySeedRowAsync(setup, candidate, probe, ct)) { target = candidate; break; }
            }
            Assert.NotNull(target);

            // Orden 1: retire primero (sin commit); el INSERT concurrente espera y, al confirmar la baja, recibe PM409.
            var org1 = await NewOrgAsync(setup, "active", ct);
            await using (var a = await OpenAsync(cs, ct))
            await using (var b = await OpenAsync(cs, ct))
            {
                await using var txA = await a.BeginTransactionAsync(ct);
                await ExecAsync(a, $"UPDATE core.organizations SET \"Status\"='retired' WHERE \"Id\"='{org1}'", ct, txA);
                var insert = TryInsertAsync(b, target!, org1, ct);
                await Task.Delay(700, ct);
                Assert.False(insert.IsCompleted, "el INSERT debe esperar el lock de la fila de la organizacion");
                await txA.CommitAsync(ct);
                Assert.Equal("PM409", await insert.WaitAsync(TimeSpan.FromSeconds(20), ct));
            }

            // Orden 2: INSERT primero (sin commit); la baja espera al INSERT y no se pierde la guardia.
            var org2 = await NewOrgAsync(setup, "active", ct);
            await using (var a = await OpenAsync(cs, ct))
            await using (var b = await OpenAsync(cs, ct))
            {
                await ExecAsync(a, $"ALTER TABLE {Q(target!.Schema)}.{Q(target.Table)} ENABLE ALWAYS TRIGGER a00_workshop_retired_guard", ct);
                await using var txB = await b.BeginTransactionAsync(ct);
                // El INSERT minimo puede violar NOT NULL/CHECK, pero el trigger BEFORE ya tomo FOR SHARE sobre la fila de la
                // organizacion y lo conserva hasta cerrar la transaccion: eso es lo que debe serializar con la baja.
                var first = await StateOfAsync(() => SeedInTxAsync(b, target!, org2, txB, ct));
                Assert.Null(first);
                var retire = ExecAsync(a, $"UPDATE core.organizations SET \"Status\"='retired' WHERE \"Id\"='{org2}'", ct);
                await Task.Delay(700, ct);
                Assert.False(retire.IsCompleted, "la baja debe esperar al INSERT en curso");
                await txB.RollbackAsync(ct);
                await retire.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
        }
        finally { await DropAsync(name, admin); }
    }

    [Fact]
    public async Task Down_removes_every_trigger_and_function()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var (name, cs, admin) = await CreateDatabaseAsync(ct);
        try
        {
            await ExecAsync(await OpenAsync(cs, ct), PMGM.Api.Modules.SystemConfiguration.WorkshopRetiredGuardSql.Down, ct);
            await using var c = await OpenAsync(cs, ct);
            Assert.Equal(0, await ScalarAsync<int>(c, "SELECT count(*)::int FROM pg_trigger WHERE tgname='a00_workshop_retired_guard'", ct));
            Assert.Equal(0, await ScalarAsync<int>(c, "SELECT count(*)::int FROM pg_proc WHERE proname IN ('workshop_retired_guard','install_workshop_retired_guard')", ct));
        }
        finally { await DropAsync(name, admin); }
    }
}
