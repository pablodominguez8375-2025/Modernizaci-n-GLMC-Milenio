using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// Synthetic PostgreSQL checks for #694. Every test mutation, including audit
/// rows, is rolled back by one outer transaction. NO real CSV is imported.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class InitialLoadBatchLedgerPostgreSqlTests
{
    [Fact]
    public async Task Unique_key_audit_fk_terminal_transitions_and_rollback()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var coreOptions = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString).Options;
        var bootstrapOptions = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseNpgsql(connectionString).Options;
        await using (var core = new PmgmDbContext(coreOptions))
            await core.Database.MigrateAsync(ct);
        await using var db = new BootstrapDbContext(bootstrapOptions);
        await db.Database.MigrateAsync(ct);

        // Random synthetic 64-char stand-in for a server-derived HMAC.
        var fingerprint = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var reservedId = Guid.NewGuid();
        var appliedId = Guid.NewGuid();
        var reversedId = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '15s'", ct);

            db.AuditEvents.Add(Audit(reservedId, at, "system.initial_load.batch_reserve"));
            await db.SaveChangesAsync(ct);
            db.InitialLoadBatchLedgers.Add(new InitialLoadBatchLedger
            {
                Fingerprint = fingerprint,
                Status = InitialLoadBatchLedger.Pending,
                Version = 0,
                ReservedAuditEventId = reservedId,
                CreatedAtUtc = at,
                UpdatedAtUtc = at
            });
            await db.SaveChangesAsync(ct);

            var pending = await db.InitialLoadBatchLedgers.AsNoTracking()
                .SingleAsync(x => x.Fingerprint == fingerprint, ct);
            Assert.Equal("pending", pending.Status);
            Assert.Equal(0L, pending.Version);
            Assert.Equal(reservedId, pending.ReservedAuditEventId);

            // An identical batch cannot be reserved a second time, even when
            // a future caller ignores the pure application policy entirely.
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT duplicate_batch", ct);
            var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO core.initial_load_batch_ledger
                        ("Fingerprint", "Status", "Version",
                         "ReservedAuditEventId", "CreatedAtUtc", "UpdatedAtUtc")
                    SELECT "Fingerprint", "Status", "Version",
                           "ReservedAuditEventId", "CreatedAtUtc", "UpdatedAtUtc"
                    FROM core.initial_load_batch_ledger
                    WHERE "Fingerprint" = {fingerprint}
                    """, ct));
            Assert.Equal("23505", duplicate.SqlState);
            await db.Database.ExecuteSqlRawAsync(
                "ROLLBACK TO SAVEPOINT duplicate_batch", ct);

            // A reserved record cannot point to a nonexistent audit event.
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT foreign_audit", ct);
            var orphan = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO core.initial_load_batch_ledger
                        ("Fingerprint", "Status", "Version",
                         "ReservedAuditEventId", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES ({new string('b', 64)}, {"pending"}, {0L},
                            {Guid.NewGuid()}, {at}, {at})
                    """, ct));
            Assert.Equal("23503", orphan.SqlState);
            await db.Database.ExecuteSqlRawAsync(
                "ROLLBACK TO SAVEPOINT foreign_audit", ct);

            var row = await db.InitialLoadBatchLedgers
                .SingleAsync(x => x.Fingerprint == fingerprint, ct);
            var appliedAt = at.AddSeconds(1);
            db.AuditEvents.Add(Audit(appliedId, appliedAt,
                "system.initial_load.batch_apply"));
            await db.SaveChangesAsync(ct);
            row.Status = InitialLoadBatchLedger.Applied;
            row.Version = 1;
            row.AppliedAuditEventId = appliedId;
            row.AppliedAtUtc = appliedAt;
            row.UpdatedAtUtc = appliedAt;
            await db.SaveChangesAsync(ct);

            var reversedAt = appliedAt.AddSeconds(1);
            db.AuditEvents.Add(Audit(reversedId, reversedAt,
                "system.initial_load.batch_reverse"));
            await db.SaveChangesAsync(ct);
            row.Status = InitialLoadBatchLedger.Reversed;
            row.Version = 2;
            row.ReversedAuditEventId = reversedId;
            row.ReversedAtUtc = reversedAt;
            row.UpdatedAtUtc = reversedAt;
            await db.SaveChangesAsync(ct);

            var final = await db.InitialLoadBatchLedgers.AsNoTracking()
                .SingleAsync(x => x.Fingerprint == fingerprint, ct);
            Assert.Equal("reversed", final.Status);
            Assert.Equal(2L, final.Version);
            Assert.Equal(reservedId, final.ReservedAuditEventId);
            Assert.Equal(appliedId, final.AppliedAuditEventId);
            Assert.Equal(reversedId, final.ReversedAuditEventId);

            // Database trigger, not an HTTP flag, forbids replay and deletion.
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT impossible_reopen", ct);
            var reopened = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE core.initial_load_batch_ledger
                       SET "Status" = 'applied', "Version" = 1
                     WHERE "Fingerprint" = {fingerprint}
                    """, ct));
            Assert.Equal("55000", reopened.SqlState);
            await db.Database.ExecuteSqlRawAsync(
                "ROLLBACK TO SAVEPOINT impossible_reopen", ct);

            await db.Database.ExecuteSqlRawAsync("SAVEPOINT impossible_delete", ct);
            var deleted = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM core.initial_load_batch_ledger
                     WHERE "Fingerprint" = {fingerprint}
                    """, ct));
            Assert.Equal("55000", deleted.SqlState);
            await db.Database.ExecuteSqlRawAsync(
                "ROLLBACK TO SAVEPOINT impossible_delete", ct);

            await transaction.RollbackAsync(ct);
        }

        // Assert no synthetic ledger or audit events leaked after rollback.
        await using var readback = new BootstrapDbContext(bootstrapOptions);
        Assert.Equal(0, await readback.InitialLoadBatchLedgers.AsNoTracking()
            .CountAsync(x => x.Fingerprint == fingerprint, ct));
        Assert.Equal(0, await readback.AuditEvents.AsNoTracking().CountAsync(
            x => x.Id == reservedId || x.Id == appliedId || x.Id == reversedId, ct));
    }

    private static AuditEvent Audit(Guid id, DateTimeOffset when, string action) => new()
    {
        Id = id,
        OccurredAtUtc = when,
        Action = action,
        EntityType = "InitialLoadBatchLedger",
        EntityId = "synthetic-test-batch",
        ActorSubject = "synthetic-master",
        CorrelationId = "synthetic-ci",
        Result = "success",
        MetadataJson = "{\"synthetic\":true}"
    };
}
