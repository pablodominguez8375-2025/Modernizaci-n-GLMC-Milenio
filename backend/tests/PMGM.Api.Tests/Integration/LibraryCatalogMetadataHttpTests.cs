using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LibraryCatalogMetadataHttpTests
{
    [Fact]
    public async Task Catalog_metadata_keeps_work_paper_short_description_separate_from_book_abstract_and_is_searchable()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new DocumentManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var collectionId = await CreateCollectionAsync(client, suffix, cancellationToken);

        var workPaperId = await CreateDocumentAsync(
            client,
            collectionId,
            $"Plancha catalogada {suffix}",
            "work_paper",
            cancellationToken);

        var invalidWorkPaperMetadata = await client.PutAsJsonAsync(
            $"/api/documentos/{workPaperId}/metadatos-biblioteca",
            new
            {
                minimumDegreeRequired = 2,
                authorName = "Hno. Autor de Prueba",
                authorLodgeName = "R∴L∴S∴ Taller de Prueba N° 23",
                documentDate = "2026-09-11",
                shortDescription = "Descripción corta de la plancha.",
                abstractText = "Una plancha no debe aceptar abstract."
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidWorkPaperMetadata.StatusCode);
        Assert.Contains(
            "no utiliza abstract",
            await invalidWorkPaperMetadata.Content.ReadAsStringAsync(cancellationToken),
            StringComparison.OrdinalIgnoreCase);

        var validWorkPaperMetadata = await client.PutAsJsonAsync(
            $"/api/documentos/{workPaperId}/metadatos-biblioteca",
            new
            {
                minimumDegreeRequired = 2,
                authorName = "Hno. Autor de Prueba",
                authorLodgeName = "R∴L∴S∴ Taller de Prueba N° 23",
                documentDate = "2026-09-11",
                shortDescription = $"Descripción corta única {suffix}"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, validWorkPaperMetadata.StatusCode);
        var workPaperJson = await validWorkPaperMetadata.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal("WorkPaper", workPaperJson.GetProperty("catalogKind").GetString());
        Assert.Equal(2, workPaperJson.GetProperty("minimumDegreeRequired").GetInt32());
        Assert.Equal($"Descripción corta única {suffix}", workPaperJson.GetProperty("shortDescription").GetString());
        Assert.Equal(JsonValueKind.Null, workPaperJson.GetProperty("abstractText").ValueKind);

        var bookId = await CreateDocumentAsync(
            client,
            collectionId,
            $"Libro catalogado {suffix}",
            "book",
            cancellationToken);
        var uniqueBookNeedle = $"abstract-biblioteca-{suffix}";
        var bookMetadata = await client.PutAsJsonAsync(
            $"/api/documentos/{bookId}/metadatos-biblioteca",
            new
            {
                minimumDegreeRequired = (int?)null,
                authorName = "Autora de Prueba",
                topic = $"Historia masónica {suffix}",
                edition = "2ª edición",
                abstractText = $"Este es el {uniqueBookNeedle} del libro de prueba."
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, bookMetadata.StatusCode);
        await MakeAvailableAndPublishAsync(client, bookId, cancellationToken);

        var officialId = await CreateDocumentAsync(
            client,
            collectionId,
            $"Constitución catalogada {suffix}",
            "official_document",
            cancellationToken);
        var officialMetadata = await client.PutAsJsonAsync(
            $"/api/documentos/{officialId}/metadatos-biblioteca",
            new
            {
                minimumDegreeRequired = (int?)null,
                documentDate = "2026-01-01",
                officialDocumentType = "Constitución"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, officialMetadata.StatusCode);
        await MakeAvailableAndPublishAsync(client, officialId, cancellationToken);

        var searchResponse = await client.GetAsync(
            $"/api/biblioteca/buscar?q={Uri.EscapeDataString(uniqueBookNeedle)}&pageSize=10",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var searchJson = await searchResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, searchJson.GetProperty("total").GetInt32());
        var bookResult = searchJson.GetProperty("items")[0];
        Assert.Equal(bookId, bookResult.GetProperty("id").GetGuid());
        Assert.Equal("Autora de Prueba", bookResult.GetProperty("authorName").GetString());
        Assert.Equal($"Historia masónica {suffix}", bookResult.GetProperty("topic").GetString());
        Assert.Equal("2ª edición", bookResult.GetProperty("edition").GetString());
        Assert.Contains(uniqueBookNeedle, bookResult.GetProperty("abstractText").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, bookResult.GetProperty("shortDescription").ValueKind);

        var officialSearch = await client.GetAsync(
            "/api/biblioteca/buscar?documentType=official_document&officialDocumentType=Constituci%C3%B3n&pageSize=10",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, officialSearch.StatusCode);
        var officialSearchText = await officialSearch.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains(officialId.ToString(), officialSearchText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Constitución", officialSearchText, StringComparison.Ordinal);

        var facets = await client.GetStringAsync("/api/biblioteca/facetas", cancellationToken);
        Assert.Contains($"Historia masónica {suffix}", facets, StringComparison.Ordinal);
        Assert.Contains("Constitución", facets, StringComparison.Ordinal);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var documentDb = verificationScope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        var persistedWorkPaper = await documentDb.InstitutionalDocuments.AsNoTracking()
            .SingleAsync(x => x.Id == workPaperId, cancellationToken);
        Assert.Equal("Hno. Autor de Prueba", persistedWorkPaper.AuthorName);
        Assert.Equal("R∴L∴S∴ Taller de Prueba N° 23", persistedWorkPaper.AuthorLodgeName);
        Assert.Equal($"Descripción corta única {suffix}", persistedWorkPaper.ShortDescription);
        Assert.Null(persistedWorkPaper.AbstractText);
        Assert.Equal(2, persistedWorkPaper.MinimumDegreeRequired);
    }

    private static async Task<Guid> CreateCollectionAsync(
        HttpClient client,
        string suffix,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/documentos/colecciones",
            new
            {
                code = $"CAT-META-{suffix}",
                name = $"Biblioteca catálogo {suffix}",
                description = "Prueba de metadatos semánticos de Biblioteca Virtual",
                scope = DocumentManagementCodes.Scope.Order,
                organizationId = (Guid?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return json.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateDocumentAsync(
        HttpClient client,
        Guid collectionId,
        string title,
        string documentType,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/documentos/colecciones/{collectionId}/documentos",
            new
            {
                title,
                documentType,
                classification = DocumentManagementCodes.Classification.Internal,
                accessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return json.GetProperty("id").GetGuid();
    }

    private static async Task MakeAvailableAndPublishAsync(
        HttpClient client,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var versionResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/versiones",
            new
            {
                originalFileName = $"catalogo-{Guid.NewGuid():N}.txt",
                contentType = "text/plain",
                sizeBytes = 64
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        var versionJson = await versionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var versionId = versionJson.GetProperty("id").GetGuid();

        await TransitionAsync(client, versionId, DocumentManagementCodes.ProcessingStatus.Uploaded, new string('b', 64), null, cancellationToken);
        await TransitionAsync(client, versionId, DocumentManagementCodes.ProcessingStatus.Scanning, null, null, cancellationToken);
        await TransitionAsync(client, versionId, DocumentManagementCodes.ProcessingStatus.Available, null, $"scan-catalog-{Guid.NewGuid():N}-clean", cancellationToken);

        var publishResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/publicar",
            new { versionId },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
    }

    private static async Task TransitionAsync(
        HttpClient client,
        Guid versionId,
        string targetStatus,
        string? sha256,
        string? scanReference,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/documentos/versiones/{versionId}/estado",
            new { targetStatus, sha256, scanReference },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
