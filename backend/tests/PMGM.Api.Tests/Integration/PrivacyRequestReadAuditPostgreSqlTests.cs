using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PrivacyRequestReadAuditPostgreSqlTests
{
    [Fact]
    public async Task ListingDataSubjectRequestsPersistsReadAuditWithoutSubjectIdentifiers()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var correlationId = $"privacy-read-test-{Guid.NewGuid():N}";
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var response = await client.GetAsync("/api/privacy/data-subject-requests", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(JsonValueKind.Array, responseBody.ValueKind);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var metadata = await verificationDb.AuditEvents.AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .Select(x => x.MetadataJson)
            .SingleAsync(cancellationToken);

        Assert.NotNull(metadata);
        using var document = JsonDocument.Parse(metadata);
        var root = document.RootElement;
        Assert.Equal(responseBody.GetArrayLength(), root.GetProperty("resultCount").GetInt32());
        Assert.False(root.TryGetProperty("PersonId", out _));
        Assert.False(root.TryGetProperty("MemberId", out _));
    }
}
