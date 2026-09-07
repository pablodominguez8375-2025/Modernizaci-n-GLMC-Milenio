using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Privacy.Entities;
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

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(options);

        await db.Database.MigrateAsync(cancellationToken);

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        Assert.Empty(pendingMigrations);

        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260907122500_InitialCore", appliedMigrations);
        Assert.Contains("20260907132500_AddCeremonyEligibility", appliedMigrations);
        Assert.Contains("20260907141000_AddAuditAndPrivacyCompliance", appliedMigrations);
        Assert.Contains("20260907154500_AddRetentionHoldsAndEvaluations", appliedMigrations);
        Assert.Contains("20260907155000_ExpandPrivacyIncidentWorkflow", appliedMigrations);
        Assert.Contains("20260907160000_AddRetentionExecutionState", appliedMigrations);

        var organization = new Organization
        {
            Name = $"CI PostgreSQL {Guid.NewGuid():N}",
            Number = "CI",
            Type = "workshop"
        };

        db.Organizations.Add(organization);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var persisted = await db.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Id == organization.Id, cancellationToken);

        Assert.Equal(organization.Name, persisted.Name);
        Assert.Equal(organization.Number, persisted.Number);
        Assert.Equal(organization.Type, persisted.Type);

        _ = await db.CandidatePublications.AsNoTracking().CountAsync(cancellationToken);
        _ = await db.AuditEvents.AsNoTracking().CountAsync(cancellationToken);
        _ = await db.PrivacySecurityIncidents.AsNoTracking().CountAsync(cancellationToken);
        _ = await db.Set<DataRetentionEvaluation>().AsNoTracking().CountAsync(cancellationToken);
    }
}
