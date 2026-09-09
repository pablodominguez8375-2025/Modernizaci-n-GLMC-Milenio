using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PMGM.Api.Data;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LibraryCatalogHttpTests
{
    [Fact]
    public async Task Catalog_search_filters_visible_publications_and_minimizes_metadata()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new DocumentManagementWebApplicationFactory(connectionString);
        using var logCollector = new TestLogCollector();
        factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(logCollector);
        using var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var collectionId = await CreateCollectionAsync(client, $"B16-{suffix}", $"Biblioteca v0.16 {suffix}", cancellationToken);

        var historyType = $"history_{suffix}";
        var formationType = $"formation_{suffix}";
        var hiddenType = $"hidden_{suffix}";
        var uniqueNeedle = $"busqueda-{suffix}";

        var historyDocumentId = await CreateDocumentAndAttemptPublicationAsync(
            client,
            collectionId,
            $"Historia institucional {uniqueNeedle}",
            historyType,
            DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            cancellationToken);

        await CreateDocumentAndAttemptPublicationAsync(
            client,
            collectionId,
            $"Formación masónica {suffix}",
            formationType,
            DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            cancellationToken);

        await CreateDocumentAndAttemptPublicationAsync(
            client,
            collectionId,
            $"Documento de gestión {suffix}",
            hiddenType,
            DocumentManagementCodes.AccessPolicy.ManagementOnly,
            cancellationToken,
            expectPublication: false);

        var searchResponse = await client.GetAsync(
            $"/api/biblioteca/buscar?q={Uri.EscapeDataString(uniqueNeedle)}&page=1&pageSize=10",
            cancellationToken);
        var searchText = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            searchResponse.StatusCode == HttpStatusCode.OK,
            $"La búsqueda de Biblioteca devolvió {(int)searchResponse.StatusCode} {searchResponse.StatusCode}. " +
            $"Cuerpo: {searchText}{Environment.NewLine}Logs de error:{Environment.NewLine}{logCollector.Dump()}");

        Assert.Contains(historyDocumentId.ToString(), searchText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("objectKey", searchText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sha256", searchText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scanReference", searchText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("originalFileName", searchText, StringComparison.OrdinalIgnoreCase);

        var searchJson = JsonDocument.Parse(searchText).RootElement;
        Assert.Equal(1, searchJson.GetProperty("total").GetInt32());
        Assert.Equal(1, searchJson.GetProperty("page").GetInt32());
        Assert.Equal(10, searchJson.GetProperty("pageSize").GetInt32());
        Assert.Equal(historyType, searchJson.GetProperty("items")[0].GetProperty("documentType").GetString());

        var typeResponse = await client.GetAsync(
            $"/api/biblioteca/buscar?documentType={Uri.EscapeDataString(formationType)}&pageSize=5",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, typeResponse.StatusCode);
        var typeJson = await typeResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, typeJson.GetProperty("total").GetInt32());
        Assert.Equal(formationType, typeJson.GetProperty("items")[0].GetProperty("documentType").GetString());

        var facetsResponse = await client.GetAsync("/api/biblioteca/facetas", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, facetsResponse.StatusCode);
        var facetsText = await facetsResponse.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains(historyType, facetsText, StringComparison.Ordinal);
        Assert.Contains(formationType, facetsText, StringComparison.Ordinal);
        Assert.DoesNotContain(hiddenType, facetsText, StringComparison.Ordinal);

        var invalidPageSize = await client.GetAsync("/api/biblioteca/buscar?pageSize=51", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);
    }

    private static async Task<Guid> CreateCollectionAsync(
        HttpClient client,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/documentos/colecciones",
            new
            {
                code,
                name,
                description = "Catálogo Biblioteca Virtual v0.16",
                scope = DocumentManagementCodes.Scope.Order,
                organizationId = (Guid?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return json.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateDocumentAndAttemptPublicationAsync(
        HttpClient client,
        Guid collectionId,
        string title,
        string documentType,
        string accessPolicy,
        CancellationToken cancellationToken,
        bool expectPublication = true)
    {
        var documentResponse = await client.PostAsJsonAsync(
            $"/api/documentos/colecciones/{collectionId}/documentos",
            new
            {
                title,
                documentType,
                classification = DocumentManagementCodes.Classification.Internal,
                accessPolicy
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);
        var documentJson = await documentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var documentId = documentJson.GetProperty("id").GetGuid();

        var versionResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/versiones",
            new
            {
                originalFileName = $"biblioteca-{Guid.NewGuid():N}.txt",
                contentType = "text/plain",
                sizeBytes = 64
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        var versionJson = await versionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var versionId = versionJson.GetProperty("id").GetGuid();

        await TransitionAsync(
            client,
            versionId,
            DocumentManagementCodes.ProcessingStatus.Uploaded,
            new string('a', 64),
            null,
            cancellationToken);
        await TransitionAsync(
            client,
            versionId,
            DocumentManagementCodes.ProcessingStatus.Scanning,
            null,
            null,
            cancellationToken);
        await TransitionAsync(
            client,
            versionId,
            DocumentManagementCodes.ProcessingStatus.Available,
            null,
            $"scan-library-{Guid.NewGuid():N}-clean",
            cancellationToken);

        var publishResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/publicar",
            new { versionId },
            cancellationToken);

        if (expectPublication)
        {
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, publishResponse.StatusCode);
            var conflictText = await publishResponse.Content.ReadAsStringAsync(cancellationToken);
            Assert.Contains("no permite publicarlo en Biblioteca", conflictText, StringComparison.OrdinalIgnoreCase);
        }

        return documentId;
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

    private sealed class TestLogCollector : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> entries = new();

        public ILogger CreateLogger(string categoryName) => new CollectorLogger(categoryName, entries);

        public string Dump() => entries.IsEmpty
            ? "(sin logs Error/Critical capturados)"
            : string.Join(Environment.NewLine, entries);

        public void Dispose()
        {
        }

        private sealed class CollectorLogger(
            string categoryName,
            ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                var message = $"[{logLevel}] {categoryName}: {formatter(state, exception)}";
                if (exception is not null)
                    message += Environment.NewLine + exception;
                entries.Enqueue(message);
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static NoopScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
