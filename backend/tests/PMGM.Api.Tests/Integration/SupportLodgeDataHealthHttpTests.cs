using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class SupportLodgeDataHealthHttpTests
{
    [Fact]
    public async Task Only_system_administrator_can_read_lodge_metadata()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.TallerSegundoVigilante);
        var response = await client.GetAsync(
            "/api/system/support/lodge-data-health",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_receives_only_fixed_relation_health_flags_no_member_data()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        using var response = await client.GetAsync(
            "/api/system/support/lodge-data-health", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);

        Assert.Equal("read_only_catalog", document.GetProperty("mode").GetString());
        Assert.Equal("gestion_logial_taller", document.GetProperty("scope").GetString());
        var items = document.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(5, items.Length);

        string[] expected =
        [
            "core.lodge_meetings",
            "core.lodge_instruction_sessions",
            "core.memberships",
            "core.members",
            "core.people"
        ];
        Assert.Equal(expected, items.Select(item => item.GetProperty("relation").GetString()));
        Assert.All(items, item =>
        {
            Assert.Equal(JsonValueKind.True, item.GetProperty("present").ValueKind);
            Assert.Equal(JsonValueKind.True, item.GetProperty("schemaUsable").ValueKind);
            Assert.Equal(JsonValueKind.True, item.GetProperty("selectGranted").ValueKind);
            Assert.Equal(JsonValueKind.True, item.GetProperty("requiredColumnsPresent").ValueKind);
        });

        var raw = document.ToString();
        Assert.DoesNotContain("connectionString", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("firstNames", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastNames", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("memberId", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sqlstate", raw, StringComparison.OrdinalIgnoreCase);
    }
}
