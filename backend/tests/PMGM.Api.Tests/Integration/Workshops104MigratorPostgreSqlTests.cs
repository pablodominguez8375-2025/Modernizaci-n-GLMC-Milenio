using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;
using PMGM.Qa;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>QA525: reviewed EF 100 -> 104 migrator for Workshops. Isolated database, synthetic history only.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class Workshops104MigratorPostgreSqlTests
{
    [Fact]
    public async Task Reviewed_migrator_applies_exactly_the_four_workshop_migrations_and_rejects_replay()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var name = "qa525_workshops104_" + Guid.NewGuid().ToString("N");
        var adminBuilder = new Npgsql.NpgsqlConnectionStringBuilder(cs) { Database = "postgres", Pooling = false };
        await using var admin = new Npgsql.NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync(ct);
        await using (var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            await create.ExecuteNonQueryAsync(ct);
        try
        {
            var testBuilder = new Npgsql.NpgsqlConnectionStringBuilder(cs) { Database = name, Pooling = false };
            var options = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
            await using var db = new PmgmDbContext(options);
            await db.GetService<IMigrator>().MigrateAsync(Workshops104MigrationRunner.Previous, ct);
            var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
            // The host's 100 IDs include other DbContexts sharing the history; synthetic entries stand in for them.
            for (var i = applied.Count; i < 100; i++)
            {
                var id = $"19000101_OtherContextSynthetic{i:D3}";
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO public.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({id}, {"10.0.11"})", ct);
                applied.Add(id);
            }
            Assert.Equal(Workshops104MigrationRunner.Required,
                (await db.Database.GetPendingMigrationsAsync(ct)).ToArray());
            await Workshops104MigrationRunner.MigrateExactlyAsync(db, applied.ToArray(), ct);
            var after = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            Assert.Equal(104, after.Length);
            Assert.All(Workshops104MigrationRunner.Required, id => Assert.Contains(id, after));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));

            await using var verify = new Npgsql.NpgsqlConnection(testBuilder.ConnectionString);
            await verify.OpenAsync(ct);
            async Task<long> Scalar(string sql)
            {
                await using var c = new Npgsql.NpgsqlCommand(sql, verify);
                return Convert.ToInt64(await c.ExecuteScalarAsync(ct));
            }
            Assert.Equal(1, await Scalar("select count(*) from pg_class where oid = to_regclass('core.workshop_status_transitions')"));
            Assert.Equal(3, await Scalar("select count(*) from pg_trigger where tgrelid = to_regclass('core.workshop_status_transitions') and not tgisinternal and tgenabled = 'O'"));
            Assert.Equal(1, await Scalar("select count(*) from pg_trigger where tgrelid = to_regclass('core.organizations') and tgname = 'a00_workshop_retired_guard' and tgtype = 31 and tgenabled = 'O'"));
            Assert.Equal(4, await Scalar("select count(*) from pg_attribute where attrelid = to_regclass('core.organizations') and attname in ('Status','RetiredAtUtc','RetiredReason','Origin') and not attisdropped"));

            // Replay must fail closed and change nothing.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Workshops104MigrationRunner.MigrateExactlyAsync(db, applied.ToArray(), ct));
            Assert.Equal(104, (await db.Database.GetAppliedMigrationsAsync(ct)).Count());
        }
        finally
        {
            await using var drop = new Npgsql.NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void Reviewed_migrator_requires_exact_history_and_exact_pending_migrations()
    {
        var required = Workshops104MigrationRunner.Required;
        var expected = Enumerable.Range(0, 99).Select(i => $"synthetic{i}")
            .Append(Workshops104MigrationRunner.Previous).ToArray();
        Workshops104MigrationRunner.ValidateBefore(expected, expected, required, required);
        void Rejects(string[] exp, string[] app, string[] pend, IReadOnlyCollection<string>? known = null) =>
            Assert.Throws<InvalidOperationException>(() => Workshops104MigrationRunner.ValidateBefore(exp, app, pend, known));
        Rejects(expected.Skip(1).ToArray(), expected.Skip(1).ToArray(), required);          // 99 entries
        Rejects(expected, expected.Skip(1).Append("different").ToArray(), required);         // history differs
        Rejects(expected, expected, required.Skip(1).ToArray());                             // a migration missing
        Rejects(expected, expected, required.Reverse().ToArray());                           // out of order
        Rejects(expected, expected, required.Append("20261201000000_ExtraLater").ToArray()); // extra pending
        Rejects(expected, expected, required.Append(required[0]).ToArray());                 // repeated
        Rejects(expected, expected, [.. required.Take(3), "20261119010100_AddWorkshopStatusTransitionsAppendOnly\n"]);
        Rejects(expected, expected, required, ["unrelated"]);                                // not in the assembly
        Rejects(expected.Take(99).Append(required[0]).ToArray(),
                expected.Take(99).Append(required[0]).ToArray(), required);                  // already applied
        Rejects(expected.Take(99).Append("other").ToArray(),
                expected.Take(99).Append("other").ToArray(), required);                      // Previous absent
    }
}
