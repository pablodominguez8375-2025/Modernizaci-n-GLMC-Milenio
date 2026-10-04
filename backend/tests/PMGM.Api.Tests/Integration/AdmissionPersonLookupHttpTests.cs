using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AdmissionPersonLookupHttpTests
{
    [Fact]
    public async Task Secretary_finds_retired_and_exact_other_member_without_profile_or_private_candidate_data()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AdmissionsDbContext>(); services.RemoveAll<DbContextOptions<AdmissionsDbContext>>();
            services.AddDbContext<AdmissionsDbContext>(options => options.UseNpgsql(connection));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = LookupAuth.Name;
                options.DefaultChallengeScheme = LookupAuth.Name;
                options.DefaultForbidScheme = LookupAuth.Name;
            }).AddScheme<AuthenticationSchemeOptions, LookupAuth>(LookupAuth.Name, _ => { });
        }));
        using var client = factory.CreateClient();
        var own = new Organization { Name = "Lookup propio", Type = "workshop" };
        var other = new Organization { Name = "Lookup ajeno", Type = "workshop" };
        var term = "Lookup" + Guid.NewGuid().ToString("N")[..8];
        Person Person(string last) => new() { FirstNames = term, LastNames = last, Email = "private@example.invalid", Phone = "private-phone" };
        var retiredPerson = Person("Retirado"); var otherPerson = Person("Otro");
        var external = Person("Externo"); var hidden = Person("Externo oculto"); var candidate = Person("Insinuado privado");
        var retired = new Member { Person = retiredPerson, PersonId = retiredPerson.Id, InstitutionalNumber = "LOOKUP-" + Guid.NewGuid().ToString("N") };
        var otherMember = new Member { Person = otherPerson, PersonId = otherPerson.Id, InstitutionalNumber = "LOOKUP-" + Guid.NewGuid().ToString("N") };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await core.Database.MigrateAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); await db.Database.MigrateAsync(ct);
            core.AddRange(own, other, retired, otherMember, external, hidden, candidate,
                new Membership { Member = retired, MemberId = retired.Id, Organization = own, OrganizationId = own.Id, MembershipType = "regular", Status = "closed", EndDate = new DateOnly(2026, 9, 1) },
                new Membership { Member = otherMember, MemberId = otherMember.Id, Organization = other, OrganizationId = other.Id, MembershipType = "regular", Status = "closed", EndDate = new DateOnly(2026, 9, 1) });
            await core.SaveChangesAsync(ct);
            db.AddRange(new AdmissionCase { OrganizationId = own.Id, PersonId = external.Id, AdmissionType = "incorporation", Status = "under_review", CreatedBySubject = "ci-lookup" },
                new AdmissionCase { OrganizationId = other.Id, PersonId = hidden.Id, AdmissionType = "incorporation", Status = "under_review", CreatedBySubject = "ci-lookup" });
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-CI-Organization", own.Id.ToString());
        string Path(string type, string query, Guid? org = null) => $"/api/admisiones/personas-busqueda?organizationId={org ?? own.Id}&admissionType={type}&query={Uri.EscapeDataString(query)}";
        var response = await client.GetAsync(Path("affiliation", term), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private); Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(retired.Id, item.GetProperty("memberId").GetGuid());
        Assert.Equal(retiredPerson.Id, item.GetProperty("personId").GetGuid());
        Assert.Equal(new[] { "displayName", "institutionalNumber", "memberId", "personId" }, item.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.DoesNotContain("private", json.GetRawText());
        var exact = await client.GetFromJsonAsync<JsonElement>(Path("affiliation", otherMember.InstitutionalNumber!), ct);
        Assert.Equal(otherMember.Id, Assert.Single(exact.GetProperty("items").EnumerateArray()).GetProperty("memberId").GetGuid());
        var partial = await client.GetFromJsonAsync<JsonElement>(Path("affiliation", otherMember.InstitutionalNumber![..12]), ct);
        Assert.Empty(partial.GetProperty("items").EnumerateArray());
        var externalJson = await client.GetFromJsonAsync<JsonElement>(Path("incorporation", term), ct);
        var externalItem = Assert.Single(externalJson.GetProperty("items").EnumerateArray());
        Assert.Equal(external.Id, externalItem.GetProperty("personId").GetGuid());
        Assert.Equal(JsonValueKind.Null, externalItem.GetProperty("memberId").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path("affiliation", term, other.Id), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Path("affiliation", "ab"), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Path("initiation", term), ct)).StatusCode);
        client.DefaultRequestHeaders.Add("X-CI-Role", InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Path("affiliation", term), ct)).StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var checkCore = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.False(await checkCore.Members.AnyAsync(x => x.PersonId == external.Id, ct));
    }

    private sealed class LookupAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "Admission-Lookup-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim("sub", "lookup-ci"),
                new Claim(InstitutionalClaims.Role, Request.Headers["X-CI-Role"].FirstOrDefault() ?? InstitutionalRoles.TallerSecretaria),
                new Claim(InstitutionalClaims.Organization, Request.Headers["X-CI-Organization"].FirstOrDefault() ?? Guid.Empty.ToString()) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
