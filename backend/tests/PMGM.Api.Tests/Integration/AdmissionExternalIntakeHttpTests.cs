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
public sealed class AdmissionExternalIntakeHttpTests
{
    [Fact]
    public async Task External_intake_is_atomic_private_and_separate_from_initiation()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AdmissionsDbContext>(); services.RemoveAll<DbContextOptions<AdmissionsDbContext>>();
            services.AddDbContext<AdmissionsDbContext>(options => options.UseNpgsql(connection).AddInterceptors(new FailExternalSave()));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityAuth.Name;
                options.DefaultChallengeScheme = IdentityAuth.Name;
                options.DefaultForbidScheme = IdentityAuth.Name;
            }).AddScheme<AuthenticationSchemeOptions, IdentityAuth>(IdentityAuth.Name, _ => { });
        }));
        using var client = factory.CreateClient();
        var own = new Organization { Name = "Incorporación sintética", Type = "workshop" };
        var other = new Organization { Name = "Taller ajeno sintético", Type = "workshop" };
        var id = "EXT" + Guid.NewGuid().ToString("N")[..10];
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await core.Database.MigrateAsync(ct);
            await scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>().Database.MigrateAsync(ct);
            core.AddRange(own, other); await core.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-CI-Organization", own.Id.ToString());
        var input = new CreateExternalIncorporationRequest(own.Id, "Hermana", "Externa sintética", id, "Obediencia sintética", "master", "Taller externo", "99");
        async Task<HttpResponseMessage> Post(CreateExternalIncorporationRequest payload)
            => await client.PostAsJsonAsync("/api/admisiones/incorporaciones/persona-nueva", payload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(input with { OrganizationId = other.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(input with { Degree = "inventado" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(input with { FirstNames = " " })).StatusCode);
        client.DefaultRequestHeaders.Add("X-CI-Role", InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(input)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CI-Role");
        var result = await Post(input); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.True(result.Headers.CacheControl?.Private); Assert.True(result.Headers.CacheControl?.NoStore);
        var json = await result.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.DoesNotContain(id, json.GetRawText());
        var personId = json.GetProperty("personId").GetGuid(); var caseId = json.GetProperty("id").GetGuid();
        Assert.Equal(JsonValueKind.Null, json.GetProperty("memberId").ValueKind);
        var duplicate = await Post(input with { RutOrInstitutionalId = " " + id.ToLowerInvariant().Insert(3, ".-") + " " });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.DoesNotContain(personId.ToString(), await duplicate.Content.ReadAsStringAsync(ct));
        // A failure after core.SaveChanges must roll back both the identity and the audit.
        var failId = "FAIL" + Guid.NewGuid().ToString("N")[..9];
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input with { RutOrInstitutionalId = failId, OriginObedience = "FAIL-SYNTHETIC" })).StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var coreCheck = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var saved = await coreCheck.People.SingleAsync(x => x.Id == personId, ct); Assert.Equal(id, saved.Rut);
        Assert.Equal(1, await coreCheck.People.CountAsync(x => x.Rut == id, ct));
        Assert.False(await coreCheck.People.AnyAsync(x => x.Rut == failId, ct));
        Assert.False(await coreCheck.Members.AnyAsync(x => x.PersonId == personId, ct));
        Assert.False(await coreCheck.CeremonyRequests.AnyAsync(x => x.CandidatePersonId == personId, ct));
        var cases = await check.ServiceProvider.GetRequiredService<AdmissionsDbContext>().AdmissionCases.Where(x => x.OrganizationId == own.Id).ToListAsync(ct);
        var admission = Assert.Single(cases); Assert.Equal(caseId, admission.Id); Assert.Equal(personId, admission.PersonId);
        Assert.Equal("incorporation", admission.AdmissionType); Assert.Null(admission.HasPeaceAndFriendshipPact);
        Assert.True(admission.WageIncreaseEvidenceApplies); Assert.True(admission.ExaltationEvidenceApplies);
        Assert.Null(admission.WithdrawalLetterGrantedDate); Assert.Equal("under_review", admission.Status);
        Assert.Equal(1, await coreCheck.AuditEvents.CountAsync(x => x.OrganizationId == own.Id && x.Action == "admission.external.intake.created", ct));
        // Concurrent submissions of the same new identifier create one identity/case only.
        var concurrentId = "RACE" + Guid.NewGuid().ToString("N")[..9];
        var race = await Task.WhenAll(Post(input with { RutOrInstitutionalId = concurrentId }), Post(input with { RutOrInstitutionalId = concurrentId }));
        Assert.Single(race.Where(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Single(race.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await coreCheck.People.CountAsync(x => x.Rut == concurrentId, ct));
    }

    private sealed class FailExternalSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AdmissionCase>().Any(x => x.Entity.OriginObedience == "FAIL-SYNTHETIC"))
                throw new DbUpdateException("Synthetic failure", new Npgsql.PostgresException("Synthetic conflict", "ERROR", "ERROR", "23505"));
            return ValueTask.FromResult(result);
        }
    }
    private sealed class IdentityAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "Admission-External-Intake-CI";
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
