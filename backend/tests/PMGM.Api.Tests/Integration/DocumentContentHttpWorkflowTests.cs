using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Data;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DocumentContentHttpWorkflowTests
{
    [Fact]
    public async Task Upload_scan_download_publish_and_library_download_follow_secure_lifecycle()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var objectStore = new InMemoryDocumentObjectStore();
        var scanner = new CleanDocumentMalwareScanner();

        using var baseFactory = new DocumentManagementWebApplicationFactory(connectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.RemoveAll<IDocumentMalwareScanner>();
                services.AddSingleton<IDocumentObjectStore>(objectStore);
                services.AddSingleton<IDocumentMalwareScanner>(scanner);
            }));
        using var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var collectionResponse = await client.PostAsJsonAsync(
            "/api/documentos/colecciones",
            new
            {
                code = $"BIN-CI-{Guid.NewGuid():N}"[..24],
                name = "Binarios CI",
                description = "Ciclo binario seguro",
                scope = DocumentManagementCodes.Scope.Order,
                organizationId = (Guid?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, collectionResponse.StatusCode);
        var collectionJson = await collectionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var collectionId = collectionJson.GetProperty("id").GetGuid();

        var documentResponse = await client.PostAsJsonAsync(
            $"/api/documentos/colecciones/{collectionId}/documentos",
            new
            {
                title = "Documento binario CI",
                documentType = "test_binary",
                classification = DocumentManagementCodes.Classification.Internal,
                accessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);
        var documentJson = await documentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var documentId = documentJson.GetProperty("id").GetGuid();

        var payload = Encoding.UTF8.GetBytes("Contenido seguro del Proyecto Milenio");
        var versionResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/versiones",
            new
            {
                originalFileName = "documento-ci.txt",
                contentType = "text/plain",
                sizeBytes = payload.LongLength
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        var versionJson = await versionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var versionId = versionJson.GetProperty("id").GetGuid();

        using var uploadContent = new ByteArrayContent(payload);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var uploadResponse = await client.PutAsync(
            $"/api/documentos/versiones/{versionId}/contenido",
            uploadContent,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var uploadJson = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Uploaded, uploadJson.GetProperty("processingStatus").GetString());
        Assert.True(uploadJson.GetProperty("hasIntegrityHash").GetBoolean());
        Assert.True(objectStore.ContainsDocumentVersion(versionId));

        using var duplicateContent = new ByteArrayContent(payload);
        duplicateContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var duplicateResponse = await client.PutAsync(
            $"/api/documentos/versiones/{versionId}/contenido",
            duplicateContent,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var prematureDownload = await client.GetAsync(
            $"/api/documentos/versiones/{versionId}/contenido",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, prematureDownload.StatusCode);

        var scanResponse = await client.PostAsync(
            $"/api/documentos/versiones/{versionId}/analizar",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, scanResponse.StatusCode);
        var scanJson = await scanResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available, scanJson.GetProperty("processingStatus").GetString());
        Assert.True(scanJson.GetProperty("clean").GetBoolean());
        Assert.Equal(1, scanner.ScanCount);

        var managedDownload = await client.GetAsync(
            $"/api/documentos/versiones/{versionId}/contenido",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, managedDownload.StatusCode);
        Assert.Equal(payload, await managedDownload.Content.ReadAsByteArrayAsync(cancellationToken));

        var publishResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/publicar",
            new { versionId },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var libraryDownload = await client.GetAsync(
            $"/api/biblioteca/{documentId}/contenido",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, libraryDownload.StatusCode);
        Assert.Equal(payload, await libraryDownload.Content.ReadAsByteArrayAsync(cancellationToken));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var documentDb = verificationScope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        var persistedVersion = await documentDb.DocumentVersions.AsNoTracking()
            .SingleAsync(x => x.Id == versionId, cancellationToken);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available, persistedVersion.ProcessingStatus);
        Assert.True(DocumentIntegrity.IsValidSha256(persistedVersion.Sha256));
        Assert.StartsWith("clamav:test-clean:", persistedVersion.ScanReference, StringComparison.Ordinal);
    }
}

internal sealed class InMemoryDocumentObjectStore : IDocumentObjectStore
{
    private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public Task StoreAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_objects.ContainsKey(objectKey))
            throw new InvalidOperationException("No se permite sobrescribir un objeto documental.");

        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _objects.Add(objectKey, buffer.ToArray());
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_objects.TryGetValue(objectKey, out var bytes))
            throw new FileNotFoundException("Objeto documental no encontrado.", objectKey);

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_objects.ContainsKey(objectKey));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _objects.Remove(objectKey);
        return Task.CompletedTask;
    }

    public bool ContainsDocumentVersion(Guid versionId)
        => _objects.Keys.Any(x => x.EndsWith(versionId.ToString("N"), StringComparison.Ordinal));
}

internal sealed class CleanDocumentMalwareScanner : IDocumentMalwareScanner
{
    public int ScanCount { get; private set; }

    public Task<DocumentMalwareScanResult> ScanAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScanCount++;
        return Task.FromResult(new DocumentMalwareScanResult(
            true,
            $"clamav:test-clean:{ScanCount}",
            null));
    }
}
