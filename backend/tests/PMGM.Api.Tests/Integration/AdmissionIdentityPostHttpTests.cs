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
public sealed class AdmissionIdentityPostHttpTests
{
    [Fact]
    public async Task Direct_post_rejects_identity_bypass_and_preserves_authorized_existing_identities()
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
                options.DefaultAuthenticateScheme = IdentityAuth.Name;
                options.DefaultChallengeScheme = IdentityAuth.Name;
                options.DefaultForbidScheme = IdentityAuth.Name;
            }).AddScheme<AuthenticationSchemeOptions, IdentityAuth>(IdentityAuth.Name, _ => { });
        }));
        using var client = factory.CreateClient();
        var own = new Organization { Name = "POST identidad propio", Type = "workshop" };
        var other = new Organization { Name = "POST identidad ajeno", Type = "workshop" };
        var order = new Organization { Name = "POST identidad Orden", Type = "order" };
        Person Person(string last) => new() { FirstNames = "Identidad sintética", LastNames = last };
        var external = Person("Externa autorizada"); var hidden = Person("Externa ajena"); var unscoped = Person("Privada sin expediente");
        var registered = Person("Hermano existente");
        var member = new Member { Person = registered, PersonId = registered.Id, InstitutionalNumber = "POST-" + Guid.NewGuid().ToString("N") };
        var initial = new AdmissionCase { OrganizationId = own.Id, PersonId = external.Id, AdmissionType = "incorporation", Status = "under_review", CreatedBySubject = "ci-identity" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await core.Database.MigrateAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); await db.Database.MigrateAsync(ct);
            core.AddRange(own, other, order, external, hidden, unscoped, member); await core.SaveChangesAsync(ct);
            db.AddRange(initial,
                new AdmissionCase { OrganizationId = other.Id, PersonId = hidden.Id, AdmissionType = "incorporation", Status = "under_review", CreatedBySubject = "ci-identity" },
                new AdmissionCase { OrganizationId = own.Id, PersonId = registered.Id, AdmissionType = "incorporation", Status = "resolved", CreatedBySubject = "ci-identity" });
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-CI-Organization", own.Id.ToString());
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        CreateAdmissionCaseRequest Incorporation(Guid person, Guid? organization = null, Guid? memberId = null, DateOnly? crv = null)
            => new(organization ?? own.Id, "incorporation", null, memberId, person, null, "Taller sintético externo", null,
                "Obediencia sintética", "master", false, false, true, null, null, crv);
        async Task AssertStatus(CreateAdmissionCaseRequest payload, HttpStatusCode expected)
            => Assert.Equal(expected, (await client.PostAsJsonAsync("/api/admisiones/expedientes", payload, ct)).StatusCode);

        await AssertStatus(Incorporation(hidden.Id), HttpStatusCode.Forbidden);
        await AssertStatus(Incorporation(unscoped.Id), HttpStatusCode.Forbidden);
        await AssertStatus(Incorporation(Guid.NewGuid()), HttpStatusCode.Forbidden);
        await AssertStatus(Incorporation(external.Id, other.Id), HttpStatusCode.Forbidden);
        await AssertStatus(Incorporation(registered.Id), HttpStatusCode.BadRequest);
        await AssertStatus(Incorporation(external.Id, memberId: member.Id), HttpStatusCode.BadRequest);
        await AssertStatus(Incorporation(external.Id, crv: today), HttpStatusCode.BadRequest);
        var affiliation = new CreateAdmissionCaseRequest(own.Id, "affiliation", "simple", member.Id, external.Id,
            null, null, null, null, null, false, false, null, null, null, today);
        await AssertStatus(affiliation, HttpStatusCode.BadRequest); // Mismatched Person/Member.
        await AssertStatus(affiliation with { PersonId = registered.Id, MemberId = null }, HttpStatusCode.BadRequest);
        client.DefaultRequestHeaders.Add("X-CI-Role", InstitutionalRoles.TallerTesoreria);
        await AssertStatus(Incorporation(external.Id), HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-CI-Role");
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>().AdmissionCases
                .CountAsync(x => x.PersonId == external.Id || x.PersonId == hidden.Id || x.PersonId == registered.Id || x.PersonId == unscoped.Id, ct));

        var valid = await client.PostAsJsonAsync("/api/admisiones/expedientes", Incorporation(external.Id), ct);
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
        var json = await valid.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(external.Id, json.GetProperty("personId").GetGuid());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("memberId").ValueKind);
        await AssertStatus(affiliation with { PersonId = registered.Id }, HttpStatusCode.Created);
        client.DefaultRequestHeaders.Add("X-CI-Role", InstitutionalRoles.GranSecretaria);
        client.DefaultRequestHeaders.Add("X-CI-Scope", "order");
        await AssertStatus(Incorporation(external.Id, order.Id), HttpStatusCode.NotFound);
        await AssertStatus(Incorporation(unscoped.Id), HttpStatusCode.Created); // Existing central authority retained.
        await using var check = factory.Services.CreateAsyncScope();
        var coreCheck = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.False(await coreCheck.Members.AnyAsync(x => x.PersonId == external.Id || x.PersonId == unscoped.Id, ct));
        Assert.Equal(member.Id, (await coreCheck.Members.SingleAsync(x => x.PersonId == registered.Id, ct)).Id);
        var cases = await check.ServiceProvider.GetRequiredService<AdmissionsDbContext>().AdmissionCases.AsNoTracking()
            .Where(x => x.PersonId == external.Id || x.PersonId == hidden.Id || x.PersonId == registered.Id || x.PersonId == unscoped.Id).ToListAsync(ct);
        Assert.Equal(6, cases.Count);
        Assert.Contains(cases, x => x.Id == initial.Id);
    }

    private sealed class IdentityAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "Admission-Identity-POST-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim("sub", "identity-ci"),
                new Claim(InstitutionalClaims.Role, Request.Headers["X-CI-Role"].FirstOrDefault() ?? InstitutionalRoles.TallerSecretaria),
                new Claim(InstitutionalClaims.Organization, Request.Headers["X-CI-Organization"].FirstOrDefault() ?? Guid.Empty.ToString()),
                new Claim(InstitutionalClaims.Scope, Request.Headers["X-CI-Scope"].FirstOrDefault() ?? "workshop") };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
