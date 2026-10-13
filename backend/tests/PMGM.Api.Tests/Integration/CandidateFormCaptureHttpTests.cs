using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
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

    private static object Capture(int version, int previous, string value = PrivateValue)
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
        var historicalFields = historicData.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Single(historicalFields);
        Assert.Equal("custom_note", historicalFields[0].GetProperty("key").GetString());
        Assert.Equal("Observación", historicalFields[0].GetProperty("label").GetString());
        Assert.DoesNotContain(historicalFields, x => x.GetProperty("isCore").GetBoolean());
        var exported = await client.GetAsync(route + "/1/export.csv", ct);
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal("text/csv", exported.Content.Headers.ContentType?.MediaType);
        Assert.True(exported.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", exported.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("attachment", exported.Content.Headers.ContentDisposition?.DispositionType);
        var csv = await exported.Content.ReadAsStringAsync(ct);
        Assert.Contains("Observación", csv, StringComparison.Ordinal);
        Assert.Contains(PrivateValue, csv, StringComparison.Ordinal);
        Assert.Contains("restricted", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("first_names", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("rut_or_institutional_id", csv, StringComparison.Ordinal);
        var wordExport = await client.GetAsync(route + "/1/export.docx", ct);
        Assert.Equal(HttpStatusCode.OK, wordExport.StatusCode);
        Assert.Equal(CandidateFormCaptureDocx.MediaType,
            wordExport.Content.Headers.ContentType?.MediaType);
        Assert.True(wordExport.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", wordExport.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("attachment", wordExport.Content.Headers.ContentDisposition?.DispositionType);
        var wordBytes = await wordExport.Content.ReadAsByteArrayAsync(ct);
        using (var docxStream = new MemoryStream(wordBytes))
        using (var archive = new ZipArchive(docxStream, ZipArchiveMode.Read))
        {
            Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
            var wordDocument = archive.GetEntry("word/document.xml");
            Assert.NotNull(wordDocument);
            using var reader = new StreamReader(wordDocument!.Open());
            var wordXml = await reader.ReadToEndAsync(ct);
            Assert.Contains(PrivateValue, wordXml, StringComparison.Ordinal);
            Assert.Contains("Observación", wordXml, StringComparison.Ordinal);
            Assert.DoesNotContain("rut_or_institutional_id", wordXml, StringComparison.Ordinal);
            Assert.DoesNotContain("first_names", wordXml, StringComparison.Ordinal);
        }


        var pdfExport = await client.GetAsync(route + "/1/export.pdf", ct);
        Assert.Equal(HttpStatusCode.OK, pdfExport.StatusCode);
        Assert.Equal(CandidateFormCapturePdf.MediaType,
            pdfExport.Content.Headers.ContentType?.MediaType);
        Assert.True(pdfExport.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", pdfExport.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("attachment", pdfExport.Content.Headers.ContentDisposition?.DispositionType);
        var pdfContent = Encoding.Latin1.GetString(await pdfExport.Content.ReadAsByteArrayAsync(ct));
        Assert.StartsWith("%PDF-1.4", pdfContent, StringComparison.Ordinal);
        var pdfText = string.Join(" ", Regex.Matches(pdfContent, @"<([0-9A-F]+)> Tj")
            .Select(match => Encoding.Latin1.GetString(
                Convert.FromHexString(match.Groups[1].Value))));
        Assert.Contains("Observación", pdfText, StringComparison.Ordinal);
        Assert.Contains(PrivateValue, pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("first_names", pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("rut_or_institutional_id", pdfText, StringComparison.Ordinal);

        var replay = await client.PostAsJsonAsync(route, Capture(1, 0), ct);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);

        var coreOverwrite = await client.PostAsJsonAsync(route, new
        {
            schemaVersion = 1, expectedCaptureRevision = 1,
            values = new { first_names = "No autorizado", custom_note = "nota" }
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, coreOverwrite.StatusCode);

        // The Treasurer belongs to the SAME workshop but has no access to
        // private candidate dossiers: organization membership is not sufficient.
        Authenticate(client, orgA.Id, InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/schema", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1/export.csv", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1/export.docx", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1/export.pdf", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(route, Capture(1, 1), ct)).StatusCode);

        Authenticate(client, orgB.Id);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1/export.csv", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1/export.docx", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(route + "/1/export.pdf", ct)).StatusCode);
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
        // Historical export stays pinned to its original schema after a new publication.
        var oldExport = await client.GetAsync(route + "/1/export.csv", ct);
        Assert.Equal(HttpStatusCode.OK, oldExport.StatusCode);
        var oldText = await oldExport.Content.ReadAsStringAsync(ct);
        Assert.Contains("\"Observación\";\"custom_note\"", oldText, StringComparison.Ordinal);
        Assert.Contains(PrivateValue, oldText, StringComparison.Ordinal);
        var oldWord = await client.GetAsync(route + "/1/export.docx", ct);
        Assert.Equal(HttpStatusCode.OK, oldWord.StatusCode);
        using (var stream = new MemoryStream(await oldWord.Content.ReadAsByteArrayAsync(ct)))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
            var xml = await reader.ReadToEndAsync(ct);
            Assert.Contains("Observación", xml, StringComparison.Ordinal);
            Assert.Contains("Versión del formulario: 1", xml, StringComparison.Ordinal);
            Assert.Contains(PrivateValue, xml, StringComparison.Ordinal);
        }


        var oldPdf = await client.GetAsync(route + "/1/export.pdf", ct);
        Assert.Equal(HttpStatusCode.OK, oldPdf.StatusCode);
        var originalPdf = Encoding.Latin1.GetString(await oldPdf.Content.ReadAsByteArrayAsync(ct));
        var originalText = string.Join(" ", Regex.Matches(originalPdf, @"<([0-9A-F]+)> Tj")
            .Select(match => Encoding.Latin1.GetString(
                Convert.FromHexString(match.Groups[1].Value))));
        Assert.Contains("Observación", originalText, StringComparison.Ordinal);
        Assert.Contains("Versión del esquema: 1", originalText, StringComparison.Ordinal);
        Assert.Contains(PrivateValue, originalText, StringComparison.Ordinal);

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
            var exports = await db.AuditEvents.AsNoTracking()
                .Where(x => x.EntityType == nameof(CandidateFormCapture) &&
                    x.Action == "candidate.form_capture.export_csv" &&
                    x.OrganizationId == orgA.Id)
                .ToListAsync(ct);
            Assert.Equal(2, exports.Count);
            var wordExports = await db.AuditEvents.AsNoTracking()
                .Where(x => x.EntityType == nameof(CandidateFormCapture) &&
                    x.Action == "candidate.form_capture.export_docx" &&
                    x.OrganizationId == orgA.Id)
                .ToListAsync(ct);
            Assert.Equal(2, wordExports.Count);
            var pdfExports = await db.AuditEvents.AsNoTracking()
                .Where(x => x.EntityType == nameof(CandidateFormCapture) &&
                    x.Action == "candidate.form_capture.export_pdf" &&
                    x.OrganizationId == orgA.Id)
                .ToListAsync(ct);
            Assert.Equal(2, pdfExports.Count);
            Assert.All(pdfExports, x =>
            {
                Assert.Contains("pdf", x.MetadataJson ?? string.Empty, StringComparison.Ordinal);
                Assert.DoesNotContain(PrivateValue,
                    x.MetadataJson ?? string.Empty, StringComparison.Ordinal);
            });
            Assert.All(wordExports, x =>
            {
                Assert.Contains("docx", x.MetadataJson ?? string.Empty, StringComparison.Ordinal);
                Assert.DoesNotContain(PrivateValue, x.MetadataJson ?? string.Empty,
                    StringComparison.Ordinal);
            });
            Assert.All(exports, x =>
            {
                Assert.DoesNotContain(PrivateValue, x.MetadataJson ?? string.Empty,
                    StringComparison.Ordinal);
                Assert.Contains("csv", x.MetadataJson ?? string.Empty, StringComparison.Ordinal);
            });

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
    public async Task ConcurrentCaptureRequestsOnlyAppendOneRevisionAndAuditOnce()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(Connection);

        var organization = new Organization { Name = "Taller simultáneo sintético", Type = "workshop" };
        var candidate = new Person { FirstNames = "Concurrente", LastNames = "Sintético" };
        var ceremony = new CeremonyRequest
        {
            Organization = organization, CandidatePerson = candidate,
            CeremonyType = CeremonyCodes.Type.Initiation,
            Status = CeremonyCodes.RequestStatus.UnderReview
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var intake = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
            await db.Database.MigrateAsync(ct);
            await intake.Database.MigrateAsync(ct);
            db.AddRange(organization, candidate, ceremony);
            await db.SaveChangesAsync(ct);
            intake.CandidateIntakeProfiles.Add(new CandidateIntakeProfile
            {
                CeremonyRequestId = ceremony.Id, OrganizationId = organization.Id,
                PersonId = candidate.Id, InsinuationDate = new DateOnly(2026, 10, 1),
                SubmittedBySubject = "concurrent-synthetic",
                UpdatedBySubject = "concurrent-synthetic"
            });
            await intake.SaveChangesAsync(ct);
            await new FormSchemaVersionStore(db).PublishAsync(
                Schema(1, DateTimeOffset.UtcNow.AddDays(-1)), organization.Id,
                FormSchemaInstitutionalCatalog.TrustedCoreKeys(
                    FormSchemaInstitutionalCatalog.CandidateProfile),
                "ci-admin", "capture-concurrency", ct);
        }

        // Two independent authenticated HTTP requests deliberately submit the
        // same expected revision. Advisory locking must serialize the writes.
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        Authenticate(clientA, organization.Id);
        Authenticate(clientB, organization.Id);
        var route = $"/api/insinuados/solicitudes/{ceremony.Id}/formularios/capturas";
        const string firstValue = "Concurrente privada A";
        const string secondValue = "Concurrente privada B";
        var responses = await Task.WhenAll(
            clientA.PostAsJsonAsync(route, Capture(1, 0, firstValue), ct),
            clientB.PostAsJsonAsync(route, Capture(1, 0, secondValue), ct));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var capture = await db.CandidateFormCaptures.AsNoTracking()
                .SingleAsync(row => row.CeremonyRequestId == ceremony.Id, ct);
            Assert.Equal(1, capture.CaptureRevision);
            var audits = await db.AuditEvents.AsNoTracking()
                .Where(row => row.EntityType == nameof(CandidateFormCapture) &&
                    row.Action == "candidate.form_capture.created" &&
                    row.OrganizationId == organization.Id)
                .ToListAsync(ct);
            Assert.Single(audits);
            Assert.DoesNotContain(firstValue, audits[0].MetadataJson ?? string.Empty,
                StringComparison.Ordinal);
            Assert.DoesNotContain(secondValue, audits[0].MetadataJson ?? string.Empty,
                StringComparison.Ordinal);
        }
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
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync(route + "/1/export.csv", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync(route + "/1/export.docx", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync(route + "/1/export.pdf", ct)).StatusCode);
        Authenticate(client, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(route, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(route + "/1/export.csv", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(route + "/1/export.docx", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(route + "/1/export.pdf", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(route, Capture(1, 0), ct)).StatusCode);
    }
}
