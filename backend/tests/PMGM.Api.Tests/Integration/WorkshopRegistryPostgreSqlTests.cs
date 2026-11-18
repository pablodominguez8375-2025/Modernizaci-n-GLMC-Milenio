using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class WorkshopRegistryPostgreSqlTests
{
    [Fact]
    public async Task Migration_adds_state_columns_and_footprint_detects_children_and_ignores_empty_workshops()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(ct);
        Assert.Contains("20261118150000_AddWorkshopRegistryState", await db.Database.GetAppliedMigrationsAsync(ct));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var parent = new Organization { Name = $"Taller padre {suffix}", Type = "workshop", Origin = "initial_load" };
        var child = new Organization { Name = $"Taller hijo {suffix}", Type = "workshop", ParentOrganizationId = parent.Id };
        var empty = new Organization { Name = $"Taller vacío {suffix}", Type = "workshop" };
        db.Organizations.AddRange(parent, empty);
        await db.SaveChangesAsync(ct);
        db.Organizations.Add(child);
        await db.SaveChangesAsync(ct);

        var withData = await WorkshopRegistryEndpoints.OrganizationsWithDataAsync(db, ct);
        Assert.Contains(parent.Id, withData);
        Assert.DoesNotContain(empty.Id, withData);
        Assert.DoesNotContain(child.Id, withData);

        var stored = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == empty.Id, ct);
        Assert.Equal("active", stored.Status);
        Assert.Equal("other", stored.Origin);
        Assert.Equal("initial_load", (await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == parent.Id, ct)).Origin);

        // La restricción de la base rechaza estados desconocidos.
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE core.organizations SET \"Status\" = 'borrado' WHERE \"Id\" = {0}", [empty.Id], ct));
    }
}
