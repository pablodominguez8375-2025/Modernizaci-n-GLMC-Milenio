using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

public sealed class PostgreSqlMigrationTests
{
    [Fact]
    public async Task Migrations_apply_and_basic_roundtrip_works()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(options);

        await db.Database.MigrateAsync();

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(appliedMigrations);

        var organization = new Organization
        {
            Name = $"CI PostgreSQL {Guid.NewGuid():N}",
            Number = "CI",
            Type = "workshop"
        };

        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var persisted = await db.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Id == organization.Id);

        Assert.Equal(organization.Name, persisted.Name);
        Assert.Equal(organization.Number, persisted.Number);
        Assert.Equal(organization.Type, persisted.Type);
    }
}
