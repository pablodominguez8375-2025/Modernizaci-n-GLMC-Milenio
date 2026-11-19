using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Tests.Integration;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Rehearsal;

/// <summary>
/// ENSAYO AISLADO del corte de Sistema › Talleres (#1035). NO es una prueba de CI: solo corre si PMGM_REHEARSAL_POSTGRES está definida y
/// apunta a una base LOCAL cuyo nombre empieza por «rehearsal_» (identidad comprobada y prohibición de apuntar a QA real).
/// Reproduce, sobre una copia con el esquema base de QA (da274d65), la ventana única de las 4 migraciones en orden, con lock_timeout,
/// duración medida, rol NO dueño, concurrencia, idempotencia del instalador de guardia, respaldo y restauración en copia, y flags apagados.
/// Evidencia en PMGM_REHEARSAL_EVIDENCE (archivo de texto). No toca ni conoce ninguna conexión de QA real.
/// </summary>
public sealed class WorkshopCutoverRehearsalTests
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("PMGM_REHEARSAL_POSTGRES");
    private static readonly string? EvidencePath = Environment.GetEnvironmentVariable("PMGM_REHEARSAL_EVIDENCE");
    private const string BaseMigration = "20261030223000_SeedSupportWorkshop00";   // última migración de PmgmDbContext en da274d65 (QA real)
    private static readonly string[] Delta =
    [
        "20261118150000_AddWorkshopRegistryState", "20261118170000_AddWorkshopRetiredGuard",
        "20261119010000_AddWorkshopStatusTransitions", "20261119010100_AddWorkshopStatusTransitionsAppendOnly"
    ];
    private static readonly StringBuilder Evidence = new();
    private static void Log(string line) { Evidence.AppendLine(line); Console.WriteLine("[ensayo] " + line); }

    private static DbContext Make(Type type, string connection)
    {
        var builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(type);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(builderType)!;
        builder.UseNpgsql(connection);
        return (DbContext)Activator.CreateInstance(type, builder.Options)!;
    }

    private static readonly Type[] OtherContexts =
    [
        typeof(CandidateIntakeDbContext), typeof(BootstrapDbContext), typeof(AdmissionsDbContext), typeof(NotificationDbContext),
        typeof(DocumentManagementDbContext), typeof(GrandArchiveDbContext), typeof(LodgeManagementDbContext), typeof(CalendarDbContext),
        typeof(GrandSecretariatDbContext), typeof(RegimenInteriorDbContext)
    ];

    private static async Task<T> Scalar<T>(NpgsqlConnection c, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, c); return (T)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task Exec(string connection, string sql, CancellationToken ct)
    {
        await using var c = new NpgsqlConnection(connection); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c); await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string AsRole(NpgsqlConnectionStringBuilder csb, string user, string password)
    {
        var copy = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Username = user, Password = password };
        return copy.ConnectionString;
    }

    private static async Task<(int Exit, string Output)> Run(string file, string args, string? password)
    {
        var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true };
        if (password is not null) psi.Environment["PGPASSWORD"] = password;
        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync() + await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, output);
    }

    [Fact]
    public async Task Cutover_window_rehearsal_on_an_isolated_copy()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;   // fuera del ensayo: no hace nada
        var ct = TestContext.Current.CancellationToken;
        var csb = new NpgsqlConnectionStringBuilder(Connection);

        // 0) Identidad y aislamiento: solo base local «rehearsal_*»; jamás QA real.
        var host = csb.Host ?? "";
        Assert.True(host is "localhost" or "127.0.0.1" or "::1" || host.StartsWith('/'), "El ensayo solo corre contra un servidor local (host=" + host + ")");
        Assert.StartsWith("rehearsal_", csb.Database ?? "");
        foreach (var banned in new[] { "glmdch", "srv01", "qa525", "prod" }) Assert.DoesNotContain(banned, Connection, StringComparison.OrdinalIgnoreCase);
        await using (var id = new NpgsqlConnection(Connection))
        {
            await id.OpenAsync(ct);
            Log($"identidad: db={await Scalar<string>(id, "select current_database()", ct)} user={await Scalar<string>(id, "select current_user", ct)} server_addr={await Scalar<string>(id, "select coalesce(inet_server_addr()::text,'unix-socket')", ct)} version={await Scalar<string>(id, "show server_version", ct)}");
            Assert.False(await Scalar<bool>(id, "select exists(select 1 from pg_database where datname not like 'rehearsal\\_%' and datname not in ('postgres','template0','template1') and datname = current_database())", ct));
        }

        // 1) Esquema BASE = QA real en da274d65: PmgmDbContext en su última migración previa al delta + los demás contextos completos.
        await using (var pm = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(Connection).Options))
            await pm.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(BaseMigration, ct);
        foreach (var type in OtherContexts) { await using var other = Make(type, Connection); await other.Database.MigrateAsync(ct); }
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(ct);
            Assert.False(await Scalar<bool>(c, "select exists(select 1 from information_schema.columns where table_schema='core' and table_name='organizations' and column_name='Status')", ct));
            Assert.False(await Scalar<bool>(c, "select to_regclass('core.workshop_status_transitions') is not null", ct));
            Log("esquema base verificado: core.organizations sin Status, sin tabla de transiciones");
        }

        // 2) Datos sintéticos (volumen para medir lock y duración). Solo columnas del esquema base.
        var seedSw = Stopwatch.StartNew();
        await Exec(Connection, """
            INSERT INTO core.organizations ("Id","Name","Number","Type","CreatedAtUtc")
            SELECT gen_random_uuid(), 'Taller ensayo '||g, (1000+g)::text, 'workshop', now() FROM generate_series(1,20000) g;
            """, ct);
        Log($"datos sintéticos: 20000 Talleres en {seedSw.ElapsedMilliseconds} ms");
        Guid sample;
        await using (var c = new NpgsqlConnection(Connection)) { await c.OpenAsync(ct); sample = await Scalar<Guid>(c, "select \"Id\" from core.organizations where \"Type\"='workshop' and \"Name\"='Taller ensayo 7' limit 1", ct); }

        // 3) Respaldo (pg_dump) ANTES de la ventana, con hash y tamaño: base del rollback.
        var dump = Path.Combine(Path.GetTempPath(), $"rehearsal-{Guid.NewGuid():N}.dump");
        var dumpArgs = $"-Fc -h {csb.Host} -p {csb.Port} -U {csb.Username} -d {csb.Database} -f {dump}";
        var (dumpExit, dumpOut) = await Run("pg_dump", dumpArgs, csb.Password);
        Assert.True(dumpExit == 0, dumpOut);
        var dumpHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(dump, ct))).ToLowerInvariant();
        Log($"respaldo pg_dump: {new FileInfo(dump).Length} bytes sha256={dumpHash}");

        // 4) Ventana única: las 4 migraciones EN ORDEN con lock_timeout/statement_timeout, midiendo la duración de cada una.
        var windowCsb = new NpgsqlConnectionStringBuilder(Connection) { Options = "-c lock_timeout=5000 -c statement_timeout=120000" };
        var window = windowCsb.ConnectionString;
        foreach (var migration in Delta)
        {
            var sw = Stopwatch.StartNew();
            await using var pm = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(window).Options);
            await pm.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(migration, ct);
            Log($"migración aplicada: {migration} en {sw.ElapsedMilliseconds} ms (lock_timeout=5000 ms)");
        }
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(ct);
            Assert.Equal(0L, await Scalar<long>(c, "select count(*) from core.workshop_status_transitions", ct));   // las migraciones no escriben transiciones
            Assert.Equal(20000L, await Scalar<long>(c, "select count(*) from core.organizations where \"Status\"='active' and \"Type\"='workshop'", ct));
            Log("integridad tras ventana: 0 transiciones escritas; 20000 Talleres activos conservados");
        }

        // 5) Instalador de guardia: paso SQL privilegiado INDEPENDIENTE; idempotencia y cobertura cero.
        await using (var pm = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(Connection).Options))
        {
            await pm.Database.ExecuteSqlRawAsync("SELECT core.install_workshop_retired_guard()", ct);
            var first = (await WorkshopRegistryEndpoints.GuardCoverageMissingAsync(pm, ct)).ToList();
            await pm.Database.ExecuteSqlRawAsync("SELECT core.install_workshop_retired_guard()", ct);   // 2ª vez: idempotente
            var second = (await WorkshopRegistryEndpoints.GuardCoverageMissingAsync(pm, ct)).ToList();
            Log($"instalador de guardia: columnas sin cobertura tras 1ª ejecución={first.Count}, tras 2ª={second.Count}");
            Assert.Empty(second);
            Assert.True(await WorkshopRegistryEndpoints.TransitionTriggersInstalledAsync(pm, ct));
        }

        // 6) Rol NO dueño (el de la aplicación): permisos reales.
        const string appUser = "rehearsal_app", appPassword = "rehearsal-only";
        await Exec(Connection, $"""
            DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='{appUser}') THEN CREATE ROLE {appUser} LOGIN PASSWORD '{appPassword}' NOSUPERUSER; END IF; END $$;
            GRANT CONNECT ON DATABASE "{csb.Database}" TO {appUser};
            GRANT USAGE ON SCHEMA core TO {appUser};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA core TO {appUser};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA core TO {appUser};
            """, ct);
        var app = AsRole(csb, appUser, appPassword);
        await using (var c = new NpgsqlConnection(app))
        {
            await c.OpenAsync(ct);
            var canInstall = await Scalar<bool>(c, "select has_function_privilege('core.install_workshop_retired_guard()', 'EXECUTE')", ct);
            var canUpdate = await Scalar<bool>(c, "select has_table_privilege('core.workshop_status_transitions', 'UPDATE')", ct);
            Log($"rol no dueño {appUser}: EXECUTE install_workshop_retired_guard={canInstall}; UPDATE sobre workshop_status_transitions={canUpdate} (concedido explícitamente por GRANT ... ON ALL TABLES; el REVOKE FROM PUBLIC no lo quita)");
            Assert.False(canInstall);   // el instalador es solo del migrador
            var denied = await Assert.ThrowsAnyAsync<PostgresException>(() => new NpgsqlCommand("select core.install_workshop_retired_guard()", c).ExecuteScalarAsync(ct));
            Assert.Equal("42501", denied.SqlState);
        }
        await using (var c = new NpgsqlConnection(app))
        {
            await c.OpenAsync(ct);
            // Aun con privilegio de tabla, el TRIGGER impide UPDATE/DELETE/TRUNCATE de la tabla de transiciones al rol de la aplicación.
            await Exec(Connection, $"INSERT INTO core.workshop_status_transitions (\"Id\",\"SubjectRef\",\"PreviousStatus\",\"NextStatus\",\"EffectiveAtUtc\",\"Origin\",\"DateKnown\") VALUES (gen_random_uuid(), '{sample}', 'active', 'retired', now(), 'backfill', true)", ct);
            foreach (var sql in new[] { "update core.workshop_status_transitions set \"NextStatus\"='active'", "delete from core.workshop_status_transitions", "truncate core.workshop_status_transitions" })
            {
                var e = await Assert.ThrowsAnyAsync<PostgresException>(() => new NpgsqlCommand(sql, c).ExecuteNonQueryAsync(ct));
                Log($"rol app: «{sql}» rechazado con SQLSTATE {e.SqlState}");
                Assert.True(e.SqlState is "PM410" or "42501", e.SqlState);
            }
            // El rol de la aplicación tampoco puede escribir sobre un Taller de baja (guardia T1) ni inventar transiciones inválidas.
            var bad = await Assert.ThrowsAnyAsync<PostgresException>(() => new NpgsqlCommand($"insert into core.workshop_status_transitions (\"Id\",\"SubjectRef\",\"PreviousStatus\",\"NextStatus\",\"EffectiveAtUtc\",\"ActorSubject\",\"ReasonRef\") values (gen_random_uuid(), '{Guid.NewGuid()}', 'active','retired', now(), 'a','r')", c).ExecuteNonQueryAsync(ct));
            Assert.Equal("PM411", bad.SqlState);
        }
        await Exec(Connection, "ALTER TABLE core.workshop_status_transitions DISABLE TRIGGER a00_workshop_status_transitions_append_only; DELETE FROM core.workshop_status_transitions; ALTER TABLE core.workshop_status_transitions ENABLE TRIGGER a00_workshop_status_transitions_append_only;", ct);   // limpieza del ensayo (solo en la copia)

        // 6b) REVOKE explícito al rol REAL de la aplicación (descubierto por consulta, no literal): UPDATE/DELETE/TRUNCATE caen por PRIVILEGIO (42501) y el INSERT sigue funcionando.
        var discoveredRole = await ((Func<Task<string>>)(async () => { await using var c = new NpgsqlConnection(app); await c.OpenAsync(ct); return await Scalar<string>(c, "select current_user", ct); }))();
        Assert.Equal(appUser, discoveredRole);
        await Exec(Connection, $"REVOKE UPDATE, DELETE, TRUNCATE ON core.workshop_status_transitions FROM {discoveredRole}", ct);
        await using (var c = new NpgsqlConnection(app))
        {
            await c.OpenAsync(ct);
            Assert.True(await Scalar<bool>(c, "select has_table_privilege('core.workshop_status_transitions','INSERT')", ct));
            foreach (var priv in new[] { "UPDATE", "DELETE", "TRUNCATE" }) Assert.False(await Scalar<bool>(c, $"select has_table_privilege('core.workshop_status_transitions','{priv}')", ct));
            foreach (var sql in new[] { "update core.workshop_status_transitions set \"NextStatus\"='active'", "delete from core.workshop_status_transitions", "truncate core.workshop_status_transitions" })
            {
                var e = await Assert.ThrowsAnyAsync<PostgresException>(() => new NpgsqlCommand(sql, c).ExecuteNonQueryAsync(ct));
                Assert.Equal("42501", e.SqlState);
            }
            Log("REVOKE explícito al rol de la app: INSERT=true; UPDATE/DELETE/TRUNCATE=false por has_table_privilege y rechazados con 42501 (privilegio, antes que el trigger)");
        }

        // 7) Concurrencia REAL: 8 transacciones Serializable intentan dar de baja el mismo Taller; exactamente una gana y deja 1 transición.
        var target = await ((Func<Task<Guid>>)(async () => { await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(ct); return await Scalar<Guid>(c, "select \"Id\" from core.organizations where \"Name\"='Taller ensayo 11'", ct); }))();
        var barrier = new Barrier(8);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await using var c = new NpgsqlConnection(app); await c.OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                var status = await Scalar<string>(c, $"select \"Status\" from core.organizations where \"Id\"='{target}'", ct);
                barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                if (status != "active") { await tx.RollbackAsync(ct); return "skipped"; }
                await new NpgsqlCommand($"update core.organizations set \"Status\"='retired', \"RetiredAtUtc\"=now(), \"RetiredReason\"='ensayo concurrencia' where \"Id\"='{target}'", c, tx).ExecuteNonQueryAsync(ct);
                await new NpgsqlCommand($"insert into core.workshop_status_transitions (\"Id\",\"SubjectRef\",\"PreviousStatus\",\"NextStatus\",\"EffectiveAtUtc\",\"ActorSubject\",\"ReasonRef\") values (gen_random_uuid(), '{target}', 'active','retired', now(), 'ensayo', 'concurrencia')", c, tx).ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct); return "won";
            }
            catch (PostgresException e) { try { await tx.RollbackAsync(ct); } catch { } return "lost:" + e.SqlState; }
        })));
        Log("concurrencia (8 transacciones Serializable): " + string.Join(", ", results.GroupBy(x => x).Select(g => $"{g.Key}×{g.Count()}")));
        Assert.Equal(1, results.Count(x => x == "won"));
        await using (var c = new NpgsqlConnection(Connection))
        {
            await c.OpenAsync(ct);
            Assert.Equal(1L, await Scalar<long>(c, $"select count(*) from core.workshop_status_transitions where \"SubjectRef\"='{target}'", ct));
            Assert.Equal("retired", await Scalar<string>(c, $"select \"Status\" from core.organizations where \"Id\"='{target}'", ct));
        }
        // La baja del ensayo se revierte en la copia para no contaminar el resto.
        await Exec(Connection, $"ALTER TABLE core.workshop_status_transitions DISABLE TRIGGER a00_workshop_status_transitions_append_only; DELETE FROM core.workshop_status_transitions; ALTER TABLE core.workshop_status_transitions ENABLE TRIGGER a00_workshop_status_transitions_append_only; UPDATE core.organizations SET \"Status\"='active', \"RetiredAtUtc\"=NULL, \"RetiredReason\"=NULL WHERE \"Id\"='{target}';", ct);

        // 8) Flags APAGADOS, HTTP de extremo a extremo (default de producción): la baja es 409 y no se escribe ninguna transición.
        using (var off = new PmgmWebApplicationFactory(Connection).WithWebHostBuilder(b => b.UseSetting("WorkshopRegistry:RetireEnabled", "false").UseSetting("WorkshopRegistry:RetiredSessionEnabled", "false")
            .ConfigureTestServices(s => s.AddAuthentication(o => { o.DefaultAuthenticateScheme = RehearsalAuth.Name; o.DefaultChallengeScheme = RehearsalAuth.Name; o.DefaultForbidScheme = RehearsalAuth.Name; })
                .AddScheme<AuthenticationSchemeOptions, RehearsalAuth>(RehearsalAuth.Name, _ => { }))))
        {
            var master = off.CreateClient(); master.DefaultRequestHeaders.Add("X-Rehearsal-Actor", "master");
            var registry = await master.GetFromJsonAsync<JsonElement>("/api/system/workshops?pageSize=1", ct);
            Assert.False(registry.GetProperty("retireEnabled").GetBoolean());
            var retire = await master.PostAsJsonAsync($"/api/system/workshops/{target}/retire", new { reason = "Ensayo con bandera apagada", confirmation = "1011" }, ct);
            Log($"flags apagados: POST retire => HTTP {(int)retire.StatusCode}");
            Assert.Equal(HttpStatusCode.Conflict, retire.StatusCode);
            await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(ct);
            Assert.Equal(0L, await Scalar<long>(c, "select count(*) from core.workshop_status_transitions", ct));
            Assert.Equal("active", await Scalar<string>(c, $"select \"Status\" from core.organizations where \"Id\"='{target}'", ct));
        }

        // 9) Rollback por RESTAURACIÓN del respaldo en una copia nueva (no por Down): el estado base se recupera exacto.
        var restoreDb = "rehearsal_restore_" + Guid.NewGuid().ToString("N")[..8];
        await Exec(Connection, $"CREATE DATABASE \"{restoreDb}\"", ct);
        var (restoreExit, restoreOut) = await Run("pg_restore", $"-h {csb.Host} -p {csb.Port} -U {csb.Username} -d {restoreDb} --no-owner {dump}", csb.Password);
        Assert.True(restoreExit == 0, restoreOut);
        var restored = new NpgsqlConnectionStringBuilder(Connection) { Database = restoreDb }.ConnectionString;
        await using (var c = new NpgsqlConnection(restored))
        {
            await c.OpenAsync(ct);
            Assert.Equal(20000L, await Scalar<long>(c, "select count(*) from core.organizations where \"Type\"='workshop' and \"Name\" like 'Taller ensayo %'", ct));
            Assert.False(await Scalar<bool>(c, "select exists(select 1 from information_schema.columns where table_schema='core' and table_name='organizations' and column_name='Status')", ct));
            Assert.False(await Scalar<bool>(c, "select to_regclass('core.workshop_status_transitions') is not null", ct));
            Assert.Equal(BaseMigration, await Scalar<string>(c, "select max(\"MigrationId\") from public.\"__EFMigrationsHistory\" where \"MigrationId\" like '2026%' and \"MigrationId\" <= '" + BaseMigration + "'", ct));
            Log($"restauración del respaldo en copia {restoreDb}: 20000 Talleres, sin Status, sin tabla de transiciones, historial EF en el último previo al delta");
        }
        await Exec(Connection, $"DROP DATABASE \"{restoreDb}\" WITH (FORCE)", ct);
        File.Delete(dump);

        if (!string.IsNullOrWhiteSpace(EvidencePath)) await File.WriteAllTextAsync(EvidencePath, Evidence.ToString(), ct);
    }

    private sealed class RehearsalAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "Rehearsal";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Rehearsal-Actor"].FirstOrDefault() != "master") return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", "rehearsal-master"), new(InstitutionalClaims.Scope, "order"), new(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
