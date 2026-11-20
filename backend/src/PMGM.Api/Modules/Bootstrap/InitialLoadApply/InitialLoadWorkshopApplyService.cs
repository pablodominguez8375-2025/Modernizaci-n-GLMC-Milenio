using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Bootstrap.Entities;
using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 E1 — transactional, all-or-nothing import of WORKSHOPS only.
/// Off by default: callers must pass <c>applyEnabled</c> explicitly (a configuration
/// flag read by a future endpoint). Not mapped to any route and not registered in DI.
/// Identity is (Number, OrienteCode) via the composite policy; the CSV workshopCode is
/// local to the batch and is never persisted or matched against the database.
/// Never logs or returns submitted values: only redacted counts and stable codes.
/// Institutional authorization for real data is external to the software; using real data in an environment still needs the phase GO.
/// </summary>
public sealed class InitialLoadWorkshopApplyService(BootstrapDbContext db)
{
    public const string ReserveAction = "system.initial_load.batch_reserve";
    public const string ApplyAction = "system.initial_load.workshops_apply";
    public const string WorkshopType = "workshop";
    public const string OriginInitialLoad = "initial_load";

    public sealed record Result(
        bool Applied, bool AlreadyApplied, string Code,
        int WorkshopsCreated, int ExactSkips, int BlockedRows)
    {
        public static Result Denied(string code, int blocked = 0) =>
            new(false, false, code, 0, 0, blocked);
    }

    public async Task<Result> ApplyAsync(
        bool applyEnabled,
        string? verifiedBatchFingerprint,
        IReadOnlyList<InitialLoadWorkshopMatchPolicy.Incoming>? incoming,
        string actorSubject,
        string correlationId,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<string?>>? additionalStages = null)
    {
        if (!applyEnabled) return Result.Denied("APPLY_DISABLED");
        if (string.IsNullOrWhiteSpace(actorSubject) || string.IsNullOrWhiteSpace(correlationId))
            return Result.Denied("ACTOR_REQUIRED");

        // Cheap pure checks before taking any database lock.
        var pure = InitialLoadBatchIdempotencyPolicy.Inspect(verifiedBatchFingerprint, null);
        if (pure.Code == "INVALID_VERIFIED_FINGERPRINT")
            return Result.Denied(pure.Code);
        if (incoming is null || incoming.Count == 0)
            return Result.Denied(incoming is null ? "SNAPSHOT_UNAVAILABLE" : "BATCH_EMPTY");

        var ownsTransaction = db.Database.CurrentTransaction is null;
        IDbContextTransaction transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : db.Database.CurrentTransaction!;
        const string savepoint = "initial_load_workshops_apply";
        if (!ownsTransaction) await transaction.CreateSavepointAsync(savepoint, cancellationToken);

        try
        {
            var result = await ExecuteAsync(
                verifiedBatchFingerprint!, incoming, actorSubject, correlationId, additionalStages, cancellationToken);
            if (result.Applied)
            {
                if (ownsTransaction) await transaction.CommitAsync(cancellationToken);
                else await transaction.ReleaseSavepointAsync(savepoint, cancellationToken);
                return result;
            }

            await RollBackAsync(transaction, ownsTransaction, savepoint, cancellationToken);
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateException or PostgresException or InvalidOperationException)
        {
            // Serialization failure, deadlock or unique violation: nothing is persisted.
            await RollBackAsync(transaction, ownsTransaction, savepoint, cancellationToken);
            db.ChangeTracker.Clear();
            return Result.Denied("CONCURRENT_OR_CONFLICTING_BATCH");
        }
        finally
        {
            if (ownsTransaction) await transaction.DisposeAsync();
        }
    }

    private static async Task RollBackAsync(
        IDbContextTransaction transaction, bool owns, string savepoint, CancellationToken ct)
    {
        if (owns) await transaction.RollbackAsync(ct);
        else await transaction.RollbackToSavepointAsync(savepoint, ct);
    }

    private async Task<Result> ExecuteAsync(
        string fingerprint,
        IReadOnlyList<InitialLoadWorkshopMatchPolicy.Incoming> incoming,
        string actorSubject, string correlationId,
        Func<CancellationToken, Task<string?>>? additionalStages, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'", ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '60s'", ct);

        // 1. The window singleton must be OPEN. The same row lock serialises concurrent
        //    imports with window transitions and with the legacy bootstrap.
        var windows = await db.InitialLoadWindows.FromSqlRaw("""
            SELECT * FROM core.initial_load_windows WHERE "Id" = 1 FOR UPDATE
            """).ToListAsync(ct);
        if (windows.Count != 1) return Result.Denied("WINDOW_STATUS_UNAVAILABLE");
        var window = InitialLoadWindowStatusReader.Project(windows[0]);
        if (window.Status != InitialLoadWindow.Open) return Result.Denied("WINDOW_NOT_OPEN");

        // 2. Idempotency: a committed batch is answered without any write.
        var ledgers = await db.InitialLoadBatchLedgers.FromSqlInterpolated($"""
            SELECT * FROM core.initial_load_batch_ledger WHERE "Fingerprint" = {fingerprint} FOR UPDATE
            """).ToListAsync(ct);
        var persisted = ledgers.Count == 0 ? null : ToPersisted(ledgers[0]);
        var decision = InitialLoadBatchIdempotencyPolicy.Inspect(fingerprint, persisted);
        if (decision.AlreadyApplied)
            return new Result(false, true, "ALREADY_APPLIED", 0, 0, 0);
        if (!decision.MayProposeReservation) return Result.Denied(decision.Code);

        // 3. Re-validate identities against the live snapshot INSIDE the transaction.
        var workshops = await InitialLoadWorkshopSnapshotReader.ReadWorkshopCandidatesAsync(db, ct);
        var existing = workshops.Select(x => new InitialLoadWorkshopMatchPolicy.Existing(
            "", x.Number ?? "", x.Name, x.OrienteCode ?? "", x.City ?? "", x.Country ?? "")).ToArray();
        var plan = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(incoming, existing);
        if (!plan.Valid) return Result.Denied(plan.Code, plan.BlockedRows);

        var toCreate = incoming.Where(x =>
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(x, existing) ==
            InitialLoadExistingRecordPolicy.Match.Absent).ToArray();
        if (toCreate.Length != plan.NewRows) return Result.Denied("PLAN_MISMATCH");

        // 4. Reserve, write, audit and mark applied atomically.
        var now = DateTimeOffset.UtcNow;
        var reservedId = Guid.NewGuid();
        var appliedId = Guid.NewGuid();
        db.AuditEvents.Add(Audit(reservedId, now, ReserveAction, "reserved", actorSubject, correlationId,
            $$"""{"workshops":{{toCreate.Length}},"exact":{{plan.ExactSkips}}}"""));
        await db.SaveChangesAsync(ct);
        db.InitialLoadBatchLedgers.Add(new InitialLoadBatchLedger
        {
            Fingerprint = fingerprint, Status = InitialLoadBatchLedger.Pending, Version = 0,
            ReservedAuditEventId = reservedId, CreatedAtUtc = now, UpdatedAtUtc = now
        });
        await db.SaveChangesAsync(ct);

        foreach (var row in toCreate)
        {
            db.Organizations.Add(new Organization
            {
                Name = row.Name, Number = row.Number, Type = WorkshopType,
                OrienteCode = row.OrienteCode, City = row.City, Country = row.Country,
                Status = "active", Origin = OriginInitialLoad
            });
        }
        // Later entity stages (Hermanos, Cargos) run INSIDE this same transaction, BEFORE the
        // ledger can become applied. A non-null code denies and rolls back everything.
        if (additionalStages is not null)
        {
            await db.SaveChangesAsync(ct); // workshops must be visible to later stages in this tx
            var stageCode = await additionalStages(ct);
            if (stageCode is not null) return Result.Denied(stageCode);
        }
        db.AuditEvents.Add(Audit(appliedId, now, ApplyAction, "applied", actorSubject, correlationId,
            $$"""{"workshops":{{toCreate.Length}},"exact":{{plan.ExactSkips}}}"""));
        await db.SaveChangesAsync(ct);

        var ledger = await db.InitialLoadBatchLedgers.SingleAsync(x => x.Fingerprint == fingerprint, ct);
        ledger.Status = InitialLoadBatchLedger.Applied;
        ledger.Version = 1;
        ledger.AppliedAuditEventId = appliedId;
        ledger.AppliedAtUtc = now;
        ledger.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);

        return new Result(true, false, "APPLIED", toCreate.Length, plan.ExactSkips, 0);
    }

    private static InitialLoadBatchIdempotencyPolicy.PersistedBatch ToPersisted(InitialLoadBatchLedger l) =>
        new(l.Fingerprint,
            l.Status switch
            {
                InitialLoadBatchLedger.Pending => InitialLoadBatchIdempotencyPolicy.Status.Pending,
                InitialLoadBatchLedger.Applied => InitialLoadBatchIdempotencyPolicy.Status.Applied,
                InitialLoadBatchLedger.Reversed => InitialLoadBatchIdempotencyPolicy.Status.Reversed,
                _ => (InitialLoadBatchIdempotencyPolicy.Status)(-1)
            },
            l.Version, l.ReservedAuditEventId, l.AppliedAuditEventId, l.ReversedAuditEventId);

    private static AuditEvent Audit(Guid id, DateTimeOffset at, string action, string result,
        string actor, string correlation, string metadata) => new()
    {
        Id = id, OccurredAtUtc = at, Action = action, EntityType = "initial_load_batch", EntityId = "workshops",
        ActorSubject = actor, Result = result, CorrelationId = correlation,
        Summary = "Carga inicial de Talleres (solo conteos)", MetadataJson = metadata
    };
}
