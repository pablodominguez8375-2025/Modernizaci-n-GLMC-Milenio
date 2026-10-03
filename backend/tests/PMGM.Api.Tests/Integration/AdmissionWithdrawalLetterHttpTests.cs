using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AdmissionWithdrawalLetterHttpTests
{
    [Theory]
    [InlineData(0, "simple", true)]
    [InlineData(-4, "activation", true)]
    [InlineData(-4, "simple", false)]
    [InlineData(0, "activation", false)]
    [InlineData(1, "simple", false)]
    [InlineData(null, "simple", false)]
    public async Task Api_validates_date_and_mode_and_persists_only_valid_cases(int? monthOffset, string mode, bool allowed)
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return; // Same environment contract as existing PostgreSQL suites.
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AdmissionsDbContext>();
            services.RemoveAll<DbContextOptions<AdmissionsDbContext>>();
            services.AddDbContext<AdmissionsDbContext>(options => options.UseNpgsql(connection));
        }));
        using var client = factory.CreateClient();
        var organization = new Organization { Name = $"CRV CI {Guid.NewGuid():N}", Type = "workshop" };
        var person = new Person { FirstNames = "Hermano", LastNames = "Sintético CRV" };
        var member = new Member { Person = person, PersonId = person.Id, InstitutionalNumber = $"CRV-{Guid.NewGuid():N}" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await core.Database.MigrateAsync(ct);
            var admissions = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>();
            await admissions.Database.MigrateAsync(ct);
            core.AddRange(organization, person, member);
            await core.SaveChangesAsync(ct);
        }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        DateOnly? granted = monthOffset is null ? null : today.AddMonths(monthOffset.Value);
        var response = await client.PostAsJsonAsync("/api/admisiones/expedientes", new
        {
            organizationId = organization.Id, personId = person.Id, memberId = member.Id,
            admissionType = "affiliation", affiliationMode = mode, withdrawalLetterGrantedDate = granted
        }, ct);
        Assert.Equal(allowed ? HttpStatusCode.Created : HttpStatusCode.BadRequest, response.StatusCode);
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<AdmissionsDbContext>();
        var cases = await db.AdmissionCases.AsNoTracking().Where(x => x.PersonId == person.Id).ToListAsync(ct);
        if (!allowed) { Assert.Empty(cases); return; }
        var saved = Assert.Single(cases);
        Assert.Equal(granted, saved.WithdrawalLetterGrantedDate);
        Assert.Equal(mode, saved.AffiliationMode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(granted!.Value.ToString("yyyy-MM-dd"), json.GetProperty("withdrawalLetterGrantedDate").GetString());
        Assert.Equal(person.Id, json.GetProperty("personId").GetGuid());
        Assert.Equal(member.Id, json.GetProperty("memberId").GetGuid());
    }
}
