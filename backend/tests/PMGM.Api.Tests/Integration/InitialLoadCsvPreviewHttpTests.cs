using System.Net;
using System.Net.Http.Json;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class InitialLoadCsvPreviewHttpTests
{
    [Fact]
    public async Task Institutional_administrator_cannot_upload_any_csv_to_master_preview()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.GranLogiaAdmin);

        var sensitive = new Dictionary<string, string>
        {
            ["Talleres.csv"] = "workshopCode,number,name,orienteCode,city,country\r\n",
            ["Hermanos.csv"] = "personRef,identityType,identityNumber,firstNames,lastNames,email,workshopCode,grade,institutionalStatus\r\n" +
                "a,rut,12.345.678-5,PRIVATE_FIRST,PRIVATE_LAST,secret@example.invalid,a,3,active\r\n",
            ["CargosVigentes.csv"] = "personRef,workshopCode,officeCode,startDate\r\n"
        };

        using var response = await client.PostAsJsonAsync(
            "/api/platform/bootstrap/initial-load/preview", sensitive,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("PRIVATE_FIRST", raw);
        Assert.DoesNotContain("PRIVATE_LAST", raw);
        Assert.DoesNotContain("12.345.678", raw);
        Assert.DoesNotContain("secret@example.invalid", raw);
    }

    [Fact]
    public async Task An_invalid_body_cannot_be_parsed_before_master_authorization()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        using var body = new StringContent("{BAD_JSON_AND_PRIVATE_TOKEN",
            System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(
            "/api/platform/bootstrap/initial-load/preview", body,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("PRIVATE_TOKEN",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
