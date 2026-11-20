using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #694 E1 on REAL PostgreSQL with SYNTHETIC data only. Most tests run inside one outer
/// transaction that is always rolled back; the concurrency test uses a throw-away database.
/// ApplyEnabled stays false in the application; here it is passed explicitly.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class InitialLoadWorkshopApplyPostgreSqlTests
{
    private const string Digest = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

    private static string? Conn => Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");

    private static string Fingerprint() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private static InitialLoadWorkshopMatchPolicy.Incoming W(
        string code, string number, string oriente = "SYN", string name = "Taller sintético") =>
        new(code, number, name, oriente, "Ciudad", "CL");

    private static async Task<BootstrapDbContext> OpenDbAsync(string connection, CancellationToken ct)
    {
        await using (var core = new PmgmDbContext(
            new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connection).Options))
            await core.Database.MigrateAsync(ct);
        var db = new BootstrapDbContext(
            new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync(ct);
        return db;
    }

    private static async Task OpenWindowAsync(BootstrapDbContext db, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        db.AuditEvents.Add(new AuditEvent
        {
            Id = id, Action = "system.initial_load.window_open", EntityType = "initial_load_window",
            EntityId = "1", Result = "applied", CorrelationId = "synthetic-" + id.ToString("N")
        });
        await db.SaveChangesAsync(ct);
        var at = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE core.initial_load_windows
            SET "Status" = 'open', "Version" = 1, "LegalApprovalDigest" = {Digest},
                "OpenedAtUtc" = {at}, "OpenedAuditEventId" = {id}, "UpdatedAtUtc" = {at}
            WHERE "Id" = 1
            """, ct);
    }

    private static Task<int> CountInitialLoadOrgsAsync(BootstrapDbContext db, CancellationToken ct) =>
        db.Organizations.AsNoTracking().CountAsync(x => x.Origin == "initial_load", ct);

    [Fact]
    public async Task Disabled_flag_and_closed_window_deny_without_any_write()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await using var db = await OpenDbAsync(iso.Connection, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var service = new InitialLoadWorkshopApplyService(db);
        var batch = new[] { W("L1", "9001") };

        var disabled = await service.ApplyAsync(false, Fingerprint(), batch, "synthetic", "c-1", ct);
        Assert.False(disabled.Applied);
        Assert.Equal("APPLY_DISABLED", disabled.Code);

        var closed = await service.ApplyAsync(true, Fingerprint(), batch, "synthetic", "c-2", ct);
        Assert.Equal("WINDOW_NOT_OPEN", closed.Code);
        Assert.Equal(0, await CountInitialLoadOrgsAsync(db, ct));
        Assert.Equal(0, await db.InitialLoadBatchLedgers.CountAsync(ct));
        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Applies_workshops_atomically_with_ledger_audit_and_idempotent_replay()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await using var db = await OpenDbAsync(iso.Connection, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OpenWindowAsync(db, ct);

        var service = new InitialLoadWorkshopApplyService(db);
        var fingerprint = Fingerprint();
        var batch = new[] { W("L1", "9001"), W("L2", "9002", "SY2") };

        var first = await service.ApplyAsync(true, fingerprint, batch, "synthetic-master", "c-3", ct);
        Assert.True(first.Applied, first.Code);
        Assert.Equal(2, first.WorkshopsCreated);

        Assert.Equal(2, await CountInitialLoadOrgsAsync(db, ct));
        var created = await db.Organizations.AsNoTracking()
            .Where(x => x.Origin == "initial_load").ToListAsync(ct);
        Assert.All(created, x =>
        {
            Assert.Equal("workshop", x.Type);
            Assert.Equal("active", x.Status);
        });
        var ledger = await db.InitialLoadBatchLedgers.AsNoTracking().SingleAsync(x => x.Fingerprint == fingerprint, ct);
        Assert.Equal("applied", ledger.Status);
        Assert.Equal(1L, ledger.Version);

        // Audit metadata carries counts only, never submitted names or numbers.
        var audits = await db.AuditEvents.AsNoTracking()
            .Where(x => x.Id == ledger.ReservedAuditEventId || x.Id == ledger.AppliedAuditEventId).ToListAsync(ct);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, a =>
        {
            Assert.DoesNotContain("9001", a.MetadataJson ?? "");
            Assert.DoesNotContain("sintético", a.MetadataJson ?? "");
            Assert.DoesNotContain("9001", a.Summary ?? "");
        });

        var replay = await service.ApplyAsync(true, fingerprint, batch, "synthetic-master", "c-4", ct);
        Assert.False(replay.Applied);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal("ALREADY_APPLIED", replay.Code);
        Assert.Equal(2, await CountInitialLoadOrgsAsync(db, ct));
        Assert.Equal(1, await db.InitialLoadBatchLedgers.CountAsync(x => x.Fingerprint == fingerprint, ct));
        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task A_batch_with_one_contradictory_row_writes_nothing()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await using var db = await OpenDbAsync(iso.Connection, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OpenWindowAsync(db, ct);
        var service = new InitialLoadWorkshopApplyService(db);

        Assert.True((await service.ApplyAsync(true, Fingerprint(),
            [W("L1", "9101", "SYA", "Nombre original")], "synthetic", "c-5", ct)).Applied);
        var before = await CountInitialLoadOrgsAsync(db, ct);
        var ledgersBefore = await db.InitialLoadBatchLedgers.CountAsync(ct);

        // Same (Number, OrienteCode) with a different name = contradictory; a new valid row rides along.
        var result = await service.ApplyAsync(true, Fingerprint(),
            [W("L2", "9102", "SYB"), W("L3", "9101", "SYA", "Otro nombre")], "synthetic", "c-6", ct);
        Assert.False(result.Applied);
        Assert.Equal("BATCH_NATURAL_KEY_CONFLICT", result.Code);
        Assert.Equal(before, await CountInitialLoadOrgsAsync(db, ct));
        Assert.Equal(ledgersBefore, await db.InitialLoadBatchLedgers.CountAsync(ct));
        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task A_late_database_failure_rolls_back_every_earlier_write()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await using var db = await OpenDbAsync(iso.Connection, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OpenWindowAsync(db, ct);
        var service = new InitialLoadWorkshopApplyService(db);
        var ledgersBefore = await db.InitialLoadBatchLedgers.CountAsync(ct);
        var auditsBefore = await db.AuditEvents.CountAsync(ct);

        // A name longer than the column fails at SaveChanges AFTER the reservation was written.
        var tooLong = new string('x', 600);
        var result = await service.ApplyAsync(true, Fingerprint(),
            [W("L1", "9201", "SYC"), W("L2", "9202", "SYD", tooLong)], "synthetic", "c-7", ct);
        Assert.False(result.Applied);
        Assert.Equal("CONCURRENT_OR_CONFLICTING_BATCH", result.Code);
        Assert.Equal(0, await CountInitialLoadOrgsAsync(db, ct));
        Assert.Equal(ledgersBefore, await db.InitialLoadBatchLedgers.CountAsync(ct));
        Assert.Equal(auditsBefore, await db.AuditEvents.CountAsync(ct));
        await tx.RollbackAsync(ct);
    }

    [Fact]
    public async Task Parallel_identical_batches_apply_exactly_once_in_an_isolated_database()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        var connection = iso.Connection;
        {
            await using (var seed = await OpenDbAsync(connection, ct)) await OpenWindowAsync(seed, ct);

            var fingerprint = Fingerprint();
            var batch = new[] { W("L1", "9301", "SYE"), W("L2", "9302", "SYF") };
            async Task<InitialLoadWorkshopApplyService.Result> Run(string correlation)
            {
                await using var db = new BootstrapDbContext(
                    new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(connection).Options);
                return await new InitialLoadWorkshopApplyService(db)
                    .ApplyAsync(true, fingerprint, batch, "synthetic", correlation, ct);
            }

            var results = await Task.WhenAll(Run("p-1"), Run("p-2"), Run("p-3"));
            Assert.Equal(1, results.Count(r => r.Applied));
            Assert.All(results.Where(r => !r.Applied), r =>
                Assert.Contains(r.Code, new[] { "ALREADY_APPLIED", "CONCURRENT_OR_CONFLICTING_BATCH" }));

            await using var check = new BootstrapDbContext(
                new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(connection).Options);
            Assert.Equal(2, await CountInitialLoadOrgsAsync(check, ct));
            Assert.Equal(1, await check.InitialLoadBatchLedgers.CountAsync(x => x.Fingerprint == fingerprint, ct));
        }
    }

    [Fact]
    public async Task An_existing_workshop_without_oriente_code_blocks_the_whole_import_and_writes_nothing()
    {
        // Characterisation of the fail-closed snapshot rule (see #694 comment): a legacy
        // workshop row lacking OrienteCode makes the composite snapshot AMBIGUOUS.
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await using var db = await OpenDbAsync(iso.Connection, ct);
        await OpenWindowAsync(db, ct);
        db.Organizations.Add(new PMGM.Api.Modules.Core.Entities.Organization
        {
            Name = "Taller preexistente sintético", Number = "9400", Type = "workshop", Origin = "other"
        });
        await db.SaveChangesAsync(ct);

        var result = await new InitialLoadWorkshopApplyService(db)
            .ApplyAsync(true, Fingerprint(), [W("L1", "9401", "SYG")], "synthetic", "c-8", ct);
        Assert.False(result.Applied);
        Assert.Equal("SNAPSHOT_KEYS_AMBIGUOUS", result.Code);
        Assert.Equal(0, await CountInitialLoadOrgsAsync(db, ct));
        Assert.Equal(0, await db.InitialLoadBatchLedgers.CountAsync(ct));
    }

    /// <summary>Throw-away database so committed state never leaks into other tests.</summary>
    private sealed class IsolatedDb : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _name;
        public string Connection { get; }

        private IsolatedDb(string admin, string name, string connection)
        { _admin = admin; _name = name; Connection = connection; }

        public static async Task<IsolatedDb> CreateAsync(string adminConnection, CancellationToken ct)
        {
            var name = "pmgm_apply_" + Guid.NewGuid().ToString("N")[..12];
            await using (var conn = new NpgsqlConnection(adminConnection))
            {
                await conn.OpenAsync(ct);
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn);
                await create.ExecuteNonQueryAsync(ct);
            }
            var cs = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
            return new IsolatedDb(adminConnection, name, cs);
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var conn = new NpgsqlConnection(_admin);
            await conn.OpenAsync(CancellationToken.None);
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", conn);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
