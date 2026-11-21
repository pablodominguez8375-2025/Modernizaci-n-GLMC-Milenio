using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PMGM.Api.Data;

namespace PMGM.Qa;

/// <summary>
/// QA525: EF 100 -> 104 for Workshops (#1035). Same reviewed pattern as <see cref="Support00MigrationRunner"/>:
/// ONE DbContext (PmgmDbContext), EXACT history before and after, EXACT pending list, a single IMigrator target.
/// No API startup, bootstrap, other contexts, SQL shortcut or Down. Fail closed: any divergence throws.
/// </summary>
public static class Workshops104MigrationRunner
{
    public const string Previous = "20261030223000_SeedSupportWorkshop00";
    public static readonly string[] Required =
    [
        "20261118150000_AddWorkshopRegistryState",
        "20261118170000_AddWorkshopRetiredGuard",
        "20261119010000_AddWorkshopStatusTransitions",
        "20261119010100_AddWorkshopStatusTransitionsAppendOnly",
    ];
    public static string Target => Required[^1];

    public static void ValidateBefore(string[] expected, string[] applied, string[] pending, IReadOnlyCollection<string>? known = null)
    {
        if (expected is null || applied is null || pending is null
            || expected.Length != 100 || expected.Distinct().Count() != 100
            || expected.Any(string.IsNullOrWhiteSpace) || !expected.Contains(Previous)
            || Required.Any(expected.Contains)
            || applied.Length != 100 || applied.Distinct().Count() != 100
            || !expected.ToHashSet().SetEquals(applied)
            // Strict: exactly the four reviewed migrations, in order, and nothing else pending. A different
            // set means the signed release changed and this contract must be reviewed again.
            || !pending.SequenceEqual(Required)
            || (known is not null && Required.Any(id => !known.Contains(id))))
            throw new InvalidOperationException("WORKSHOPS104_EXACT_PENDING_HISTORY_REQUIRED");
    }

    public static async Task RunAsync(string connection, string[] expected, CancellationToken ct)
    {
        var builder = new NpgsqlConnectionStringBuilder(connection);
        // Only the schema owner may run DDL: a superuser (pmgm_app) or runtime role must never be used here.
        if (builder.Database != "pmgm" || builder.Username != "pmgm_migrator"
            || builder.Host != "postgres" || builder.Port != 5432)
            throw new InvalidOperationException("WORKSHOPS104_DDL_CONNECTION_REQUIRED");
        builder.ApplicationName = "qa525-workshops104-reviewed";
        builder.Pooling = false;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(builder.ConnectionString).Options;
        await using var db = new PmgmDbContext(options);
        await MigrateExactlyAsync(db, expected, ct);
    }

    public static async Task MigrateExactlyAsync(PmgmDbContext db, string[] expected, CancellationToken ct)
    {
        var before = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        ValidateBefore(expected, before, pending, db.Database.GetMigrations().ToArray());
        await db.GetService<IMigrator>().MigrateAsync(Target, ct);
        var after = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        if (after.Length != 104 || after.Distinct().Count() != 104
            || !before.Concat(Required).ToHashSet().SetEquals(after))
            throw new InvalidOperationException("WORKSHOPS104_POST_HISTORY_MISMATCH");
        var pendingAfter = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        if (pendingAfter.Length != 0)
            throw new InvalidOperationException("WORKSHOPS104_POST_PENDING_MISMATCH");
    }
}
