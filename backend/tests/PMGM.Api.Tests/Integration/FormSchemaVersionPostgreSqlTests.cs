using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class FormSchemaVersionPostgreSqlTests
{
    private static FormSchemaSnapshot Snapshot(string code, int version) => new(
        code,
        version,
        new DateTimeOffset(2026, 10, version, 0, 0, 0, TimeSpan.Zero),
        [new FormFieldDefinition("candidate_id", "Identidad del candidato", "reference",
            IsCore: true, Required: true, Classification: FormFieldClassification.Sensitive),
         new FormFieldDefinition("custom_note", "Anotación futura", "text",
            Classification: FormFieldClassification.Restricted)]);

    [Fact]
    public async Task ConsecutiveVersionsAreHistoricalScopedAuditedAndImmutable()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connection)
            .Options;

        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(ct);

        var lodgeA = new Organization { Name = "Taller CI formularios A", Type = "workshop" };
        var lodgeB = new Organization { Name = "Taller CI formularios B", Type = "workshop" };
        db.Organizations.AddRange(lodgeA, lodgeB);
        await db.SaveChangesAsync(ct);
        var code = "qa_form_" + Guid.NewGuid().ToString("N");
        var store = new FormSchemaVersionStore(db);

        var first = await store.PublishAsync(
            Snapshot(code, 1), lodgeA.Id, ["candidate_id"], "admin-form-ci", "form-test-a1", ct);
        var second = await store.PublishAsync(
            Snapshot(code, 2), lodgeA.Id, ["candidate_id"], "admin-form-ci", "form-test-a2", ct);

        // The same form code/version can exist in other authorized scopes,
        // but reads never cross scopes and never silently fall forward in time.
        await store.PublishAsync(
            Snapshot(code, 1), lodgeB.Id, ["candidate_id"], "admin-form-ci", "form-test-b1", ct);
        Assert.Null(await store.ReadAsync(code, 1, null, ct));
        Assert.Null(await store.ReadAsync(code, 2, lodgeB.Id, ct));
        Assert.Equal(1, (await store.ReadAsync(code, 1, lodgeA.Id, ct))!.Version);
        Assert.Equal(2, (await store.ReadAsync(code, 2, lodgeA.Id, ct))!.Version);
        Assert.Equal(3, await db.FormSchemaVersions.AsNoTracking()
            .CountAsync(x => x.FormCode == code, ct));

        // Stale clients cannot publish an old version in place.
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishAsync(
            Snapshot(code, 2), lodgeA.Id, ["candidate_id"], "admin-form-ci", "form-test-replay", ct));
        Assert.Equal(3, await db.FormSchemaVersions.AsNoTracking()
            .CountAsync(x => x.FormCode == code, ct));

        var audit = await db.AuditEvents.AsNoTracking().Where(a =>
            a.EntityType == "FormSchemaVersion" &&
            (a.EntityId == first.Id.ToString("D") || a.EntityId == second.Id.ToString("D")))
            .ToListAsync(ct);
        Assert.Equal(2, audit.Count);
        Assert.All(audit, a => Assert.Equal("system.forms.schema_version_published", a.Action));
        Assert.All(audit, a => Assert.Equal(lodgeA.Id, a.OrganizationId));

        // Even SQL writers cannot update or delete an already published version.
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.form_schema_versions WHERE \"Id\" = {first.Id}", ct));
        Assert.NotNull(await store.ReadAsync(code, 1, lodgeA.Id, ct));
    }

    [Fact]
    public async Task InvalidCoreChangesAreRejectedBeforeAnyDatabaseMutation()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connection).Options;
        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(ct);
        var code = "qa_form_" + Guid.NewGuid().ToString("N");
        var store = new FormSchemaVersionStore(db);

        var initial = Snapshot(code, 1);
        await store.PublishAsync(initial, null, ["candidate_id"], "admin-form-ci", "form-test-v1", ct);
        var removedCore = Snapshot(code, 2) with
        {
            Fields = [new FormFieldDefinition("custom_note", "Anotación futura", "text",
                Classification: FormFieldClassification.Restricted)]
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishAsync(
            removedCore, null, ["candidate_id"], "admin-form-ci", "form-test-invalid", ct));
        Assert.Equal(1, await db.FormSchemaVersions.AsNoTracking().CountAsync(
            x => x.ScopeKey == FormSchemaScope.Order && x.FormCode == code, ct));
    }
}
