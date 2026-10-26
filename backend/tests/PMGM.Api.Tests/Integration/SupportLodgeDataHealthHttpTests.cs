using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class SupportLodgeDataHealthHttpTests
{
    [Fact]
    public void Catalog_health_checks_cover_all_materialized_lodge_entity_columns()
    {
        // A ready flag must not hide a missing column selected by an EF entity query.
        // This is a model-only regression: it never opens a database connection.
        var checksField = typeof(SupportLodgeDataHealthEndpoints).GetField(
            "Checks", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(checksField);
        var checks = Assert.IsAssignableFrom<Array>(checksField!.GetValue(null));
        var requiredByRelation = checks.Cast<object>().ToDictionary(
            check => (string)check.GetType().GetProperty("Relation")!.GetValue(check)!,
            check => (string[])check.GetType().GetProperty("RequiredColumns")!.GetValue(check)!);

        var options = new DbContextOptionsBuilder<LodgeManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=not_connected;Password=not_connected")
            .Options;
        using var db = new LodgeManagementDbContext(options);

        foreach (var (clrType, table) in new[]
        {
            (typeof(LodgeMeeting), "lodge_meetings"),
            (typeof(LodgeInstructionSession), "lodge_instruction_sessions")
        })
        {
            var relation = $"core.{table}";
            Assert.True(requiredByRelation.TryGetValue(relation, out var required),
                $"No catalog check for {relation}");
            var entity = db.Model.FindEntityType(clrType);
            Assert.NotNull(entity);
            var storeObject = StoreObjectIdentifier.Table(table, "core");
            foreach (var mappedProperty in entity!.GetProperties())
            {
                var column = mappedProperty.GetColumnName(storeObject);
                Assert.NotNull(column);
                Assert.Contains(column!, required!);
            }
        }
    }

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
    public async Task Query_probe_requires_system_authority_and_does_not_echo_target_id()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.TallerSegundoVigilante);
        var id = Guid.NewGuid();

        using var denied = await client.GetAsync(
            $"/api/system/support/lodge-query-probe/{id}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // The 404 assertion must not depend on test ordering: an unmigrated
        // database would otherwise return a legitimate probe 503.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        client.DefaultRequestHeaders.Remove("X-Test-Role");
        using var absent = await client.GetAsync(
            $"/api/system/support/lodge-query-probe/{id}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.True(absent.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(id.ToString("D"),
            await absent.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Query_probe_isolated_reads_use_only_three_fixed_flags_not_personal_content()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        Guid organizationId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var institutionalDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var lodgeDb = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
            await institutionalDb.Database.MigrateAsync(ct);
            await lodgeDb.Database.MigrateAsync(ct);

            var org = new Organization
            {
                Name = "TEST ORGANIZATION PRIVATE " + Guid.NewGuid().ToString("N"),
                Type = "workshop"
            };
            var person = new Person { FirstNames = "SECRET_FNAME_681", LastNames = "SECRET_LNAME_681" };
            var member = new Member
            {
                Person = person, PersonId = person.Id,
                InstitutionalNumber = "SECRET_MEMBER_NUMBER_681"
            };
            var membership = new Membership
            {
                Member = member, MemberId = member.Id,
                Organization = org, OrganizationId = org.Id,
                MembershipType = "regular",
                Status = MembershipCodes.MembershipStatus.Active
            };
            institutionalDb.AddRange(org, person, member, membership);
            await institutionalDb.SaveChangesAsync(ct);
            organizationId = org.Id;
        }

        using var response = await client.GetAsync(
            $"/api/system/support/lodge-query-probe/{organizationId}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var payload = JsonDocument.Parse(body);
        Assert.Equal("ready", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal("read_only_queries", payload.RootElement.GetProperty("mode").GetString());
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, items.Length);
        Assert.Equal(["tenidas", "miembros", "instrucciones"],
            items.Select(x => x.GetProperty("resource").GetString()).ToArray());
        Assert.All(items, x => Assert.Equal("ok", x.GetProperty("result").GetString()));
        foreach (var forbidden in new[]
        {
            organizationId.ToString("D"), "SECRET_FNAME_681", "SECRET_LNAME_681",
            "SECRET_MEMBER_NUMBER_681", "memberId", "sqlstate", "stacktrace",
            "password", "connectionString", "exception"
        })
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
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
