using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DocumentContentRecoveryHttpTests
{
    [Fact]
    public async Task Reconciliation_recovers_valid_orphaned_object_without_overwrite()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var objectStore = new InMemoryDocumentObjectStore();

        using var baseFactory = new DocumentManagementWebApplicationFactory(connectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.AddSingleton<IDocumentObjectStore>(objectStore);
            }));
        using var client = factory.CreateClient();

        var payload = Encoding.UTF8.GetBytes("Objeto persistido antes de confirmar PostgreSQL");
        var versionId = await CreatePendingVersionAsync(
            client,
            "recuperacion.txt",
            "text/plain",
            payload.LongLength,
            cancellationToken);

        string objectKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            objectKey = await db.DocumentVersions.AsNoTracking()
                .Where(x => x.Id == versionId)
                .Select(x => x.ObjectKey)
                .SingleAsync(cancellationToken);
        }

        await using (var orphanedContent = new MemoryStream(payload, writable: false))
        {
            await objectStore.StoreAsync(objectKey, orphanedContent, "text/plain", cancellationToken);
        }

        var response = await client.PostAsync(
            $"/api/documentos/versiones/{versionId}/reconciliar-contenido",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Uploaded, json.GetProperty("processingStatus").GetString());
        Assert.True(json.GetProperty("hasIntegrityHash").GetBoolean());
        Assert.True(json.GetProperty("reconciled").GetBoolean());
        Assert.True(objectStore.ContainsDocumentVersion(versionId));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        var persisted = await verificationDb.DocumentVersions.AsNoTracking()
            .SingleAsync(x => x.Id == versionId, cancellationToken);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Uploaded, persisted.ProcessingStatus);
        Assert.True(DocumentIntegrity.IsValidSha256(persisted.Sha256));
    }

    [Fact]
    public async Task Upload_rejects_declared_pdf_when_real_signature_is_not_pdf_and_deletes_object()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var objectStore = new InMemoryDocumentObjectStore();

        using var baseFactory = new DocumentManagementWebApplicationFactory(connectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.AddSingleton<IDocumentObjectStore>(objectStore);
            }));
        using var client = factory.CreateClient();

        var fakePdf = Encoding.ASCII.GetBytes("MZ-not-a-real-pdf-document");
        var versionId = await CreatePendingVersionAsync(
            client,
            "manual.pdf",
            "application/pdf",
            fakePdf.LongLength,
            cancellationToken);

        using var uploadContent = new ByteArrayContent(fakePdf);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var response = await client.PutAsync(
            $"/api/documentos/versiones/{versionId}/contenido",
            uploadContent,
            cancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.False(objectStore.ContainsDocumentVersion(versionId));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        var persisted = await verificationDb.DocumentVersions.AsNoTracking()
            .SingleAsync(x => x.Id == versionId, cancellationToken);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.PendingUpload, persisted.ProcessingStatus);
        Assert.Null(persisted.Sha256);
    }

    private static async Task<Guid> CreatePendingVersionAsync(
        HttpClient client,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        var collectionResponse = await client.PostAsJsonAsync(
            "/api/documentos/colecciones",
            new
            {
                code = $"REC-CI-{Guid.NewGuid():N}"[..24],
                name = "Recuperación CI",
                description = "Pruebas de recuperación documental",
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
                title = "Documento recuperación CI",
                documentType = "recovery_test",
                classification = DocumentManagementCodes.Classification.Internal,
                accessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);
        var documentJson = await documentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var documentId = documentJson.GetProperty("id").GetGuid();

        var versionResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/versiones",
            new
            {
                originalFileName = fileName,
                contentType,
                sizeBytes
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        var versionJson = await versionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return versionJson.GetProperty("id").GetGuid();
    }
}
