using System.Collections.Concurrent;
using Amazon.S3;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// Real ephemeral PostgreSQL + Silo + ClamAV with the product HTTP endpoints,
/// test-only synthetic authentication, DB audit and no institutional connections.
/// This does not exercise Keycloak or production role assignments.
/// </summary>
public sealed class SiloDocumentHttpLifecycleRealProbe
{
    [Fact]
    public async Task Authenticated_test_HTTP_enforces_scan_gate_audit_and_rejects_infected_content()
    {
        if (Environment.GetEnvironmentVariable("PMGM_TEST_REQUIRE_REAL_HTTP_DOCS") != "1")
            return;

        static string Required(string key) =>
            Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException($"Falta variable sintética {key}");

        var dbConnection = Required("PMGM_TEST_POSTGRES");
        var storage = Options.Create(new DocumentStorageOptions
        {
            ServiceUrl = Required("PMGM_TEST_S3_URL"),
            AccessKey = Required("PMGM_TEST_S3_ACCESS_KEY"),
            SecretKey = Required("PMGM_TEST_S3_SECRET_KEY"),
            Bucket = Required("PMGM_TEST_S3_BUCKET"),
            ForcePathStyle = true,
            Region = "us-east-1"
        });
        var malware = Options.Create(new DocumentMalwareOptions
        {
            Host = Required("PMGM_TEST_CLAMAV_HOST"),
            Port = int.Parse(Required("PMGM_TEST_CLAMAV_PORT")),
            MaxStreamBytes = 1_048_576
        });
        var ct = TestContext.Current.CancellationToken;
        var s3Diagnostics = new ConcurrentQueue<string>();

        using var baseFactory = new DocumentManagementWebApplicationFactory(dbConnection);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.RemoveAll<IDocumentMalwareScanner>();
                services.AddSingleton<IDocumentObjectStore>(
                    new DiagnosingS3Store(new S3DocumentObjectStore(storage), s3Diagnostics));
                services.AddSingleton<IDocumentMalwareScanner>(
                    new ClamAvDocumentMalwareScanner(malware));
            }));
        using var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
        }

        var collectionResponse = await client.PostAsJsonAsync(
            "/api/documentos/colecciones",
            new
            {
                code = $"LAB-{Guid.NewGuid():N}"[..24],
                name = "Laboratorio HTTP Silo CI",
                description = "Sólo documentos sintéticos",
                scope = DocumentManagementCodes.Scope.Order,
                organizationId = (Guid?)null
            }, ct);
        Assert.Equal(HttpStatusCode.Created, collectionResponse.StatusCode);
        var collectionId = (await collectionResponse.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: ct)).GetProperty("id").GetGuid();

        async Task<(Guid DocumentId, Guid VersionId)> ReserveAsync(
            string name, byte[] payload)
        {
            var create = await client.PostAsJsonAsync(
                $"/api/documentos/colecciones/{collectionId}/documentos",
                new
                {
                    title = "Prueba documental " + Guid.NewGuid().ToString("N"),
                    documentType = "test_binary",
                    classification = DocumentManagementCodes.Classification.Sensitive,
                    accessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated
                }, ct);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var document = await create.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            var docId = document.GetProperty("id").GetGuid();

            var version = await client.PostAsJsonAsync(
                $"/api/documentos/{docId}/versiones",
                new
                {
                    originalFileName = name,
                    contentType = "text/plain",
                    sizeBytes = payload.LongLength
                }, ct);
            Assert.Equal(HttpStatusCode.Created, version.StatusCode);
            var versionJson = await version.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            return (docId, versionJson.GetProperty("id").GetGuid());
        }

        async Task UploadAsync(Guid versionId, byte[] bytes)
        {
            using var payload = new ByteArrayContent(bytes);
            payload.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            var upload = await client.PutAsync(
                $"/api/documentos/versiones/{versionId}/contenido",
                payload, ct);
            Assert.True(upload.StatusCode == HttpStatusCode.OK,
                "La subida HTTP falló: status=" + (int)upload.StatusCode +
                " S3-probe=" + string.Join(";", s3Diagnostics));
            var result = await upload.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            Assert.Equal(DocumentManagementCodes.ProcessingStatus.Uploaded,
                         result.GetProperty("processingStatus").GetString());
        }

        async Task<JsonElement> ScanAsync(HttpClient requestClient, Guid versionId,
                                          HttpStatusCode expected)
        {
            using var scan = await requestClient.PostAsync(
                $"/api/documentos/versiones/{versionId}/analizar", null, ct);
            Assert.Equal(expected, scan.StatusCode);
            return await scan.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
        }

        byte[] clean = Encoding.UTF8.GetBytes(
            "Proyecto Centenario: contenido benigno, ficticio y sin información personal.");
        byte[] eicar = Encoding.ASCII.GetBytes(
            "X5O!P%@AP[4" + @"\PZX54(P^)7CC)7}$" +
            "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

        // Authenticated synthetic institutional role, real endpoint/controller,
        // real S3 store, PostgreSQL and clamd.
        var good = await ReserveAsync("limpio-ci.txt", clean);
        await UploadAsync(good.VersionId, clean);
        using (var duplicate = new ByteArrayContent(clean))
        {
            duplicate.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            var rejectOverwrite = await client.PutAsync(
                $"/api/documentos/versiones/{good.VersionId}/contenido",
                duplicate, ct);
            Assert.Equal(HttpStatusCode.Conflict, rejectOverwrite.StatusCode);
        }
        var premature = await client.GetAsync(
            $"/api/documentos/versiones/{good.VersionId}/contenido", ct);
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        var scanClean = await ScanAsync(client, good.VersionId, HttpStatusCode.OK);
        Assert.True(scanClean.GetProperty("clean").GetBoolean());
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available,
                     scanClean.GetProperty("processingStatus").GetString());

        var publish = await client.PostAsJsonAsync(
            $"/api/documentos/{good.DocumentId}/publicar",
            new { versionId = good.VersionId }, ct);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var download = await client.GetAsync(
            $"/api/documentos/versiones/{good.VersionId}/contenido", ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var cleanRead = await download.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(clean, cleanRead);
        Assert.Equal(SHA256.HashData(clean), SHA256.HashData(cleanRead));

        var library = await client.GetAsync(
            $"/api/biblioteca/{good.DocumentId}/contenido", ct);
        Assert.Equal(HttpStatusCode.OK, library.StatusCode);
        Assert.Equal(clean, await library.Content.ReadAsByteArrayAsync(ct));

        // EICAR is the harmless antivirus test signature.
        var infected = await ReserveAsync("eicar-ci.txt", eicar);
        await UploadAsync(infected.VersionId, eicar);
        var scanThreat = await ScanAsync(client, infected.VersionId, HttpStatusCode.OK);
        Assert.False(scanThreat.GetProperty("clean").GetBoolean());
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Rejected,
                     scanThreat.GetProperty("processingStatus").GetString());
        var blockedPublish = await client.PostAsJsonAsync(
            $"/api/documentos/{infected.DocumentId}/publicar",
            new { versionId = infected.VersionId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, blockedPublish.StatusCode);
        var blockedDownload = await client.GetAsync(
            $"/api/documentos/versiones/{infected.VersionId}/contenido", ct);
        Assert.Equal(HttpStatusCode.Conflict, blockedDownload.StatusCode);

        var unscanned = await ReserveAsync("indisponible-ci.txt", clean);
        await UploadAsync(unscanned.VersionId, clean);

        // Same real PostgreSQL/Silo, second WebApplicationFactory with a
        // production scanner configured against intentionally closed localhost:1.
        // No fallback to a "clean" response.
        using var unavailableFactory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.RemoveAll<IDocumentMalwareScanner>();
                services.AddSingleton<IDocumentObjectStore>(
                    new DiagnosingS3Store(new S3DocumentObjectStore(storage), s3Diagnostics));
                services.AddSingleton<IDocumentMalwareScanner>(
                    new ClamAvDocumentMalwareScanner(
                        Options.Create(new DocumentMalwareOptions
                        {
                            Host = "127.0.0.1", Port = 1,
                            MaxStreamBytes = 1_048_576
                        })));
            }));
        using var outageClient = unavailableFactory.CreateClient();
        var outage = await ScanAsync(outageClient, unscanned.VersionId,
                                     HttpStatusCode.ServiceUnavailable);
        Assert.True(outage.TryGetProperty("message", out _));
        var cannotDownload = await outageClient.GetAsync(
            $"/api/documentos/versiones/{unscanned.VersionId}/contenido", ct);
        Assert.Equal(HttpStatusCode.Conflict, cannotDownload.StatusCode);
        var cannotPublish = await outageClient.PostAsJsonAsync(
            $"/api/documentos/{unscanned.DocumentId}/publicar",
            new { versionId = unscanned.VersionId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, cannotPublish.StatusCode);

        await using var verification = factory.Services.CreateAsyncScope();
        var dbVerification = verification.ServiceProvider
            .GetRequiredService<DocumentManagementDbContext>();
        var ids = new[] { good.VersionId, infected.VersionId, unscanned.VersionId };
        var versions = await dbVerification.DocumentVersions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
        Assert.Equal(3, versions.Count);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available,
            versions.Single(x => x.Id == good.VersionId).ProcessingStatus);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Rejected,
            versions.Single(x => x.Id == infected.VersionId).ProcessingStatus);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Scanning,
            versions.Single(x => x.Id == unscanned.VersionId).ProcessingStatus);
        Assert.StartsWith("clamav:clean:",
            versions.Single(x => x.Id == good.VersionId).ScanReference);
        Assert.StartsWith("clamav:rejected:",
            versions.Single(x => x.Id == infected.VersionId).ScanReference);
        Assert.Null(versions.Single(x => x.Id == unscanned.VersionId).ScanReference);

        var idStrings = ids.Select(id => id.ToString()).ToArray();
        var events = await dbVerification.AuditEvents.AsNoTracking()
            .Where(x => idStrings.Contains(x.EntityId))
            .ToListAsync(ct);
        foreach (var expected in new[]
        {
            "documents.content.uploaded", "documents.content.scan_clean",
            "documents.content.scan_rejected", "documents.content.scan_unavailable",
            "documents.content.downloaded", "library.content.downloaded"
        })
            Assert.Contains(events, e => e.Action == expected);
        var auditMetadata = string.Join("\n", events.Select(e => e.MetadataJson));
        Assert.DoesNotContain("EICAR-STANDARD-ANTIVIRUS-TEST-FILE", auditMetadata);
        Assert.DoesNotContain("X5O!P%@AP", auditMetadata);

        // The test container, DB and Silo are entirely destroyed by the Python
        // runner after this one real authenticated test is marked Passed.
    }
}

// Captures ONLY sanitized status, error code and request stream capability
// from the real product S3 client; never logs object names, URLs or credentials.
internal sealed class DiagnosingS3Store(
    S3DocumentObjectStore inner, ConcurrentQueue<string> notes) : IDocumentObjectStore, IDisposable
{
    private async Task<T> Attempt<T>(string operation, bool? seekable, Func<Task<T>> call)
    {
        try { return await call(); }
        catch (AmazonS3Exception ex)
        {
            string safeCode = new string((ex.ErrorCode ?? "none")
                .Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_').Take(60).ToArray());
            notes.Enqueue(operation + ":http=" + (int)ex.StatusCode +
                ":code=" + safeCode +
                (seekable.HasValue ? ":seekable=" + seekable.Value : ""));
            throw;
        }
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default)
        => Attempt("head", null, () => inner.ExistsAsync(objectKey, ct));

    public Task StoreAsync(string objectKey, Stream content, string contentType,
        CancellationToken ct = default)
        => Attempt("put", content.CanSeek, async () =>
        {
            await inner.StoreAsync(objectKey, content, contentType, ct);
            return true;
        });

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct = default)
        => Attempt("get", null, () => inner.OpenReadAsync(objectKey, ct));

    public Task DeleteAsync(string objectKey, CancellationToken ct = default)
        => Attempt("delete", null, async () =>
        {
            await inner.DeleteAsync(objectKey, ct);
            return true;
        });

    public void Dispose() => inner.Dispose();
}
