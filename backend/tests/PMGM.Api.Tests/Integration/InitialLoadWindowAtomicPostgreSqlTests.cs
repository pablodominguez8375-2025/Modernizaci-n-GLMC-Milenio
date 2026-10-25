using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// Real PostgreSQL integration test, exclusively in the synthetic CI database.
/// Both audited transitions are enclosed in ONE outer test transaction that is
/// rolled back. This does not invoke an operational endpoint or legal approval.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class InitialLoadWindowAtomicPostgreSqlTests
{
    private const string SyntheticLegalDigest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task Audit_foreign_keys_state_transitions_and_rollback_are_atomic()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var coreOptions = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString).Options;
        var bootstrapOptions = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseNpgsql(connectionString).Options;

        // Use the same migration path as the existing bootstrap PostgreSQL tests.
        await using (var core = new PmgmDbContext(coreOptions))
            await core.Database.MigrateAsync(ct);
        await using var db = new BootstrapDbContext(bootstrapOptions);
        await db.Database.MigrateAsync(ct);

        var openedAuditId = Guid.NewGuid();
        var closedAuditId = Guid.NewGuid();
        var correlationId = "synthetic-qa-" + Guid.NewGuid().ToString("N");

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '15s'", ct);

            // Same PostgreSQL lock as PR #713 and #715. Never edit this row
            // outside the rollback-only test transaction.
            var rows = await db.InitialLoadWindows.FromSqlRaw("""
                SELECT * FROM core.initial_load_windows WHERE "Id" = 1 FOR UPDATE
                """).ToListAsync(ct);
            var window = Assert.Single(rows);
            Assert.Equal(InitialLoadWindow.NotOpened, window.Status);
            Assert.Equal(0, window.Version);
            Assert.True(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(window));

            var openAt = DateTimeOffset.UtcNow.AddSeconds(2);
            var openPlan = InitialLoadWindowTransitionGuard.Plan(
                window,
                InitialLoadAdmissionPolicy.Operation.RequestOpen,
                TrustedDecision(InitialLoadAdmissionPolicy.Window.NotOpened,
                    InitialLoadAdmissionPolicy.Operation.RequestOpen),
                openAt, openedAuditId, SyntheticLegalDigest);
            Assert.True(openPlan.PreconditionsSatisfied);

            var opened = InitialLoadWindowAtomicTransition.Materialize(
                window, openPlan, openAt, openedAuditId, SyntheticLegalDigest);
            Assert.NotNull(opened);
            db.AuditEvents.Add(Audit(openedAuditId, openAt, correlationId,
                "system.initial_load.window_open"));
            await db.SaveChangesAsync(ct);
            CopyState(window, opened);
            await db.SaveChangesAsync(ct);

            var readOpen = await db.InitialLoadWindows.AsNoTracking()
                .SingleAsync(x => x.Id == InitialLoadWindow.SingletonId, ct);
            Assert.Equal(InitialLoadWindow.Open, readOpen.Status);
            Assert.Equal(1, readOpen.Version);
            Assert.Equal(openedAuditId, readOpen.OpenedAuditEventId);
            Assert.False(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(readOpen));

            var closeAt = openAt.AddSeconds(1);
            var closePlan = InitialLoadWindowTransitionGuard.Plan(
                readOpen,
                InitialLoadAdmissionPolicy.Operation.RequestClose,
                TrustedDecision(InitialLoadAdmissionPolicy.Window.Open,
                    InitialLoadAdmissionPolicy.Operation.RequestClose),
                closeAt, closedAuditId);
            Assert.True(closePlan.PreconditionsSatisfied);

            var closed = InitialLoadWindowAtomicTransition.Materialize(
                readOpen, closePlan, closeAt, closedAuditId, null);
            Assert.NotNull(closed);
            db.AuditEvents.Add(Audit(closedAuditId, closeAt, correlationId,
                "system.initial_load.window_close"));
            await db.SaveChangesAsync(ct);
            CopyState(window, closed);
            await db.SaveChangesAsync(ct);

            var readClosed = await db.InitialLoadWindows.AsNoTracking()
                .SingleAsync(x => x.Id == InitialLoadWindow.SingletonId, ct);
            Assert.Equal(InitialLoadWindow.Closed, readClosed.Status);
            Assert.Equal(2, readClosed.Version);
            Assert.Equal(openedAuditId, readClosed.OpenedAuditEventId);
            Assert.Equal(closedAuditId, readClosed.ClosedAuditEventId);
            Assert.Equal(SyntheticLegalDigest, readClosed.LegalApprovalDigest);
            Assert.Equal(2, await db.AuditEvents.CountAsync(
                x => x.Id == openedAuditId || x.Id == closedAuditId, ct));

            // PostgreSQL triggers must reject a reopening independently of
            // any HTTP/authorization service. Savepoint keeps the test's
            // transaction usable after the intentional SQLSTATE 55000.
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT verify_irreversible", ct);
            var rejected = await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlRawAsync("""
                    UPDATE core.initial_load_windows
                    SET "Status" = 'open', "Version" = 1
                    WHERE "Id" = 1
                    """, ct));
            Assert.Equal("55000", rejected.SqlState);
            await db.Database.ExecuteSqlRawAsync(
                "ROLLBACK TO SAVEPOINT verify_irreversible", ct);

            await transaction.RollbackAsync(ct);
        }

        // Independent readback: neither the window nor either audit row was
        // left behind by the synthetic test execution.
        await using var readback = new BootstrapDbContext(bootstrapOptions);
        var restored = await readback.InitialLoadWindows.AsNoTracking()
            .SingleAsync(x => x.Id == InitialLoadWindow.SingletonId, ct);
        Assert.Equal(InitialLoadWindow.NotOpened, restored.Status);
        Assert.Equal(0, restored.Version);
        Assert.Null(restored.OpenedAuditEventId);
        Assert.Null(restored.ClosedAuditEventId);
        Assert.Equal(0, await readback.AuditEvents.CountAsync(
            x => x.Id == openedAuditId || x.Id == closedAuditId, ct));
    }

    private static InitialLoadAdmissionPolicy.Decision TrustedDecision(
        InitialLoadAdmissionPolicy.Window state,
        InitialLoadAdmissionPolicy.Operation operation)
    {
        var master = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin),
            new Claim(InstitutionalClaims.Scope, "order")
        ], "synthetic-unit-test"));
        return InitialLoadAdmissionPolicy.Evaluate(
            master, new InstitutionalAccessService(), state, operation,
            new InitialLoadAdmissionPolicy.Evidence(
                MasterAccountVerifiedByServer: true,
                LegalApprovalVerifiedByServer: true,
                AuditSinkVerifiedByServer: true,
                AuditReason: "Aprobación simulada EXCLUSIVA para test PostgreSQL",
                HasPendingTransactions: false));
    }

    private static AuditEvent Audit(Guid id, DateTimeOffset when, string correlation,
        string action) => new()
    {
        Id = id,
        OccurredAtUtc = when,
        Action = action,
        EntityType = "InitialLoadWindow",
        EntityId = "1",
        ActorSubject = "synthetic-admin",
        CorrelationId = correlation,
        Result = "success",
        MetadataJson = "{\"synthetic\":true}"
    };

    private static void CopyState(InitialLoadWindow target, InitialLoadWindow source)
    {
        target.Status = source.Status;
        target.Version = source.Version;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.LegalApprovalDigest = source.LegalApprovalDigest;
        target.OpenedAtUtc = source.OpenedAtUtc;
        target.OpenedAuditEventId = source.OpenedAuditEventId;
        target.ClosedAtUtc = source.ClosedAtUtc;
        target.ClosedAuditEventId = source.ClosedAuditEventId;
    }
}
