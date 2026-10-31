using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PMGM.Api.Data;

namespace PMGM.Qa;

public static class Support00MigrationRunner
{
    public const string Target = "20261030223000_SeedSupportWorkshop00";
    public const string Previous = "20261029120000_AddMemberHistoryProposals";

    public static void ValidateBefore(string[] expected, string[] applied, string[] pending)
    {
        if (expected is null || applied is null || pending is null
            || expected.Length != 99 || expected.Distinct().Count() != 99
            || expected.Any(string.IsNullOrWhiteSpace) || !expected.Contains(Previous)
            || expected.Contains(Target) || applied.Length != 99
            || applied.Distinct().Count() != 99
            || !expected.ToHashSet().SetEquals(applied)
            || !pending.SequenceEqual([Target]))
            throw new InvalidOperationException("SUPPORT00_EXACT_PENDING_HISTORY_REQUIRED");
    }

    public static async Task RunAsync(string connection, string[] expected, CancellationToken ct)
    {
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Database != "pmgm" || string.IsNullOrWhiteSpace(builder.Username)
            || builder.Host != "postgres" || builder.Port != 5432)
            throw new InvalidOperationException("SUPPORT00_DDL_CONNECTION_REQUIRED");
        builder.ApplicationName = "qa525-support00-reviewed";
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
        ValidateBefore(expected, before, pending);
        // Target exactly ONE reviewed EF migration in ONE DbContext. No app
        // startup, bootstrap, other contexts, SQL insertion shortcut or Down.
        await db.GetService<IMigrator>().MigrateAsync(Target, ct);
        var after = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        if (after.Length != 100 || after.Distinct().Count() != 100
            || !before.Append(Target).ToHashSet().SetEquals(after))
            throw new InvalidOperationException("SUPPORT00_POST_HISTORY_MISMATCH");
    }
}
