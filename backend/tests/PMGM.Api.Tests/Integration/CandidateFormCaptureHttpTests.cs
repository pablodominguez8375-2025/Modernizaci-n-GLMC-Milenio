using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.FormConfiguration;
using PMGM.Api.Tests.Unit;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateFormCaptureHttpTests
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
    private const string PrivateValue = "Observación confidencial sintética CI-37";

    private static FormSchemaSnapshot Schema(int version, DateTimeOffset effective)
        => new(FormSchemaInstitutionalCatalog.CandidateProfile, version, effective,
            FormSchemaInstitutionalCatalogTests.ValidFields());

    private static object Capture(int version, int previous, object? value = PrivateValue)
        => new { schemaVersion = version, expectedCaptureRevision = previous,
                 values = new Dictionary<string, object?> { ["custom_note"] = value } };

    private static void Authenticate(HttpClient client, Guid organizationId,
        string role = InstitutionalRoles.TallerSecretaria, string scope = "workshop")
    {
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Organization");
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Scope");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", organizationId.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", role);
        if (scope == "order")
            client.DefaultRequestHeaders.Add("X-Publication-Test-Scope", "order");
    }

    [Fact]
    public async Task CandidateCaptureUsesPublishedSchemaAndKeepsEveryRevisionWithPrivateAudit()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(Connection);
        using var client = factory.CreateClient();

        var orgA = new Organization { Name = "Taller histórico de formularios A", Type = "workshop" };
        var orgB = new Organization { Name = "Taller histórico de formularios B", Type = "workshop" };
        var person = new Person { FirstNames = "Persona", LastNames = "Sintética" };
        var ceremony = new CeremonyRequest
        {
            Organization = orgA, CandidatePerson = person,
            CeremonyType = CeremonyCodes.Type.Initiation,
            Status = CeremonyCodes.RequestStatus.UnderReview
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var intake = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
            await db.Database.MigrateAsync(ct);
            await intake.Database.MigrateAsync(ct);
            db.AddRange(orgA, orgB, person, ceremony);
            await db.SaveChangesAsync(ct);

            intake.CandidateIntakeProfiles.Add(new CandidateIntakeProfile
            {
                CeremonyRequestId = ceremony.Id, OrganizationId = orgA.Id,
                PersonId = person.Id, InsinuationDate = new DateOnly(2026, 10, 1),
                SubmittedBySubject = "synthetic-secretary", UpdatedBySubject = "synthetic-secretary"
            });
            await intake.SaveChangesAsync(ct);

            var store = new FormSchemaVersionStore(db);
            await store.PublishAsync(Schema(1, DateTimeOffset.UtcNow.AddDays(-2)), orgA.Id,
                FormSchemaInstitutionalCatalog.TrustedCoreKeys(FormSchemaInstitutionalCatalog.CandidateProfile),
                "ci-admin", "capture-schema-v1", ct);
        }

        var route = $"/api/insinuados/solicitudes/{ceremony.Id}/formularios/capturas";
        Authenticate(client, orgA.Id);
        var activeSchema = await client.GetAsync(route + "/schema", ct);
        Assert.Equal(HttpStatusCode.OK, activeSchema.StatusCode);
        Assert.True(activeSchema.Headers.CacheControl?.NoStore);
        var activeDefinition = await activeSchema.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, activeDefinition.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(0, activeDefinition.GetProperty("expectedCaptureRevision").GetInt32());
        Assert.Single(activeDefinition.GetProperty("fields").EnumerateArray());
        Assert.Equal("custom_note", activeDefinition.GetProperty("fields")[0].GetProperty("key").GetString());
        var first = await client.PostAsJsonAsync(route, Capture(1, 0), ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstData = await first.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, firstData.GetProperty("captureRevision").GetInt32());
        Assert.Equal(1, firstData.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(64, firstData.GetProperty("payloadSha256").GetString()!.Length);

        var historic = await client.GetAsync(route + "/1", ct);
        Assert.Equal(HttpStatusCode.OK, historic.StatusCode);
        Assert.True(historic.Headers.CacheControl?.NoStore);
        var historicData = await historic.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(PrivateValue,
            historicData.GetProperty("values").GetProperty("custom_note").GetString());

        var replay = await client.PostAsJsonAsync(route, Capture(1, 0), ct);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);

        var coreOverwrite = await client.PostAsJsonAsync(route, new
        {
            schemaVersion = 1, expectedCaptureRevision = 1,
            values = new { first_names = "No autorizado", custom_note = "nota" }
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, coreOverwrite.StatusCode);

        Authenticate(client, orgB.Id);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(route, Capture(1, 1), ct)).StatusCode);
        Authenticate(client, orgA.Id);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await new FormSchemaVersionStore(db).PublishAsync(
                Schema(2, DateTimeOffset.UtcNow.AddDays(-1)), orgA.Id,
                FormSchemaInstitutionalCatalog.TrustedCoreKeys(FormSchemaInstitutionalCatalog.CandidateProfile),
                "ci-admin", "capture-schema-v2", ct);
        }

        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(route, Capture(1, 1), ct)).StatusCode);
        var second = await client.PostAsJsonAsync(route, Capture(2, 1, "Nuevo valor privado"), ct);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(route, ct);
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.False(list.GetProperty("captures")[0].TryGetProperty("values", out _));

        var original = await client.GetFromJsonAsync<JsonElement>(route + "/1", ct);
        Assert.Equal(1, original.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(PrivateValue,
            original.GetProperty("values").GetProperty("custom_note").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var captures = await db.CandidateFormCaptures.AsNoTracking()
                .Where(x => x.CeremonyRequestId == ceremony.Id)
                .OrderBy(x => x.CaptureRevision).ToListAsync(ct);
            Assert.Equal(2, captures.Count);
            Assert.NotEqual(captures[0].SchemaVersionId, captures[1].SchemaVersionId);
            var audits = await db.AuditEvents.AsNoTracking()
                .Where(x => x.EntityType == nameof(CandidateFormCapture) &&
                    x.Action == "candidate.form_capture.created")
                .Where(x => x.OrganizationId == orgA.Id)
                .ToListAsync(ct);
            Assert.Equal(2, audits.Count);
            Assert.All(audits, x => Assert.DoesNotContain(PrivateValue,
                x.MetadataJson ?? string.Empty, StringComparison.Ordinal));

            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM core.candidate_form_captures WHERE \"Id\" = {captures[0].Id}", ct));
            var persisted = await db.CandidateFormCaptures.AsNoTracking()
                .CountAsync(x => x.CeremonyRequestId == ceremony.Id, ct);
            Assert.Equal(2, persisted);

            var row = await db.CeremonyRequests.SingleAsync(x => x.Id == ceremony.Id, ct);
            row.Status = CeremonyCodes.RequestStatus.Authorized;
            await db.SaveChangesAsync(ct);
        }

        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(route, Capture(2, 2), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route + "/1", ct)).StatusCode);
    }

    [Fact]
    public async Task AnonymousOrMissingProfileCannotReadOrCreateAndLegacyProfileRemainsIndependent()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(Connection);
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
            await scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>()
                .Database.MigrateAsync(ct);
        }
        var requestId = Guid.NewGuid();
        var route = $"/api/insinuados/solicitudes/{requestId}/formularios/capturas";

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync(route, ct)).StatusCode);
        Authenticate(client, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(route, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(route, Capture(1, 0), ct)).StatusCode);
    }
}
