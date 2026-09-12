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
public sealed class DocumentManagementQueryHttpTests
{
    [Fact]
    public async Task Collection_listing_returns_management_metadata_without_storage_secrets()
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

        var collection = await client.PostAsJsonAsync("/api/documentos/colecciones", new { code = $"Q-{Guid.NewGuid():N}"[..20], name = "Consulta CI", description = (string?)null, scope = "order", organizationId = (Guid?)null }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, collection.StatusCode);
        var collectionJson = await collection.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var collectionId = collectionJson.GetProperty("id").GetGuid();

        var created = await client.PostAsJsonAsync($"/api/documentos/colecciones/{collectionId}/documentos", new { title = "Documento de consulta", documentType = "publication", classification = "internal", accessPolicy = "library_authenticated" }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await client.GetAsync($"/api/documentos/colecciones/{collectionId}/documentos", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("Documento de consulta", text);
        Assert.DoesNotContain("objectKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sha256", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("originalFileName", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scanReference", text, StringComparison.OrdinalIgnoreCase);
    }
}
