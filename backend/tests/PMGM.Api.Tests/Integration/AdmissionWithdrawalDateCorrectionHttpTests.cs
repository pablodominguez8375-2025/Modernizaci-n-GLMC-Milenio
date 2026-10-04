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
public sealed class AdmissionWithdrawalDateCorrectionHttpTests
{
    private static bool failAudit;
    [Fact]
    public async Task Date_correction_is_authorized_atomic_and_audited()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES"); if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>(); services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connection).AddInterceptors(new FailAudit()));
            services.RemoveAll<AdmissionsDbContext>(); services.RemoveAll<DbContextOptions<AdmissionsDbContext>>();
            services.AddDbContext<AdmissionsDbContext>(options => options.UseNpgsql(connection));
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = IdentityAuth.Name; options.DefaultChallengeScheme = IdentityAuth.Name; options.DefaultForbidScheme = IdentityAuth.Name; }).AddScheme<AuthenticationSchemeOptions, IdentityAuth>(IdentityAuth.Name, _ => { });
        }));
        using var client = factory.CreateClient();
        var today = AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow);
        var org = new Organization { Name = "CRV correction synthetic", Type = "workshop" };
        var c = new AdmissionCase { OrganizationId = org.Id, PersonId = Guid.NewGuid(), AdmissionType = "affiliation", AffiliationMode = "simple", WithdrawalLetterGrantedDate = today.AddDays(-1), Status = "under_review", CreatedBySubject = "synthetic" };
        var grant = today.AddMonths(-3).AddDays(-1);
        var letter = new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", DocumentVersionId = Guid.NewGuid(), EvidenceDate = grant, ReviewStatus = "approved", ReviewedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1), CreatedBySubject = "synthetic" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await core.Database.MigrateAsync(ct); core.Add(org); await core.SaveChangesAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); await db.Database.MigrateAsync(ct); db.Add(c); db.Add(letter); await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-CI-Organization", org.Id.ToString());
        var input = new WithdrawalLetterDateCorrectionRequest(letter.Id, today, "SYNTHETIC LETTER REVIEW", "Error de transcripción sintético");
        var route = $"/api/admisiones/expedientes/{c.Id}/carta-retiro/correccion-fecha";
        async Task<HttpResponseMessage> Post(WithdrawalLetterDateCorrectionRequest value) => await client.PostAsJsonAsync(route, value, ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(input)).StatusCode);
        client.DefaultRequestHeaders.Add("X-CI-Role", InstitutionalRoles.GranSecretaria); client.DefaultRequestHeaders.Add("X-CI-Scope", "order");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(input with { Reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(input with { AsOfDate = today.AddDays(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input with { EvidenceId = Guid.NewGuid() })).StatusCode);
        failAudit = true;
        try { Assert.Equal(HttpStatusCode.Conflict, (await Post(input)).StatusCode); }
        finally { failAudit = false; }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); var unchanged = await db.AdmissionCases.SingleAsync(x => x.Id == c.Id, ct);
            Assert.Equal(c.WithdrawalLetterGrantedDate, unchanged.WithdrawalLetterGrantedDate); Assert.Equal("simple", unchanged.AffiliationMode);
            Assert.Equal(0, await db.AdmissionDecisions.CountAsync(x => x.AdmissionCaseId == c.Id, ct));
        }
        var result = await Post(input); Assert.Equal(HttpStatusCode.OK, result.StatusCode); Assert.True(result.Headers.CacheControl?.NoStore);
        var json = await result.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct); Assert.Equal("activation", json.GetProperty("affiliationMode").GetString()); Assert.True(json.GetProperty("signatureReviewRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input)).StatusCode);
        await using var check = factory.Services.CreateAsyncScope(); var finalDb = check.ServiceProvider.GetRequiredService<AdmissionsDbContext>();
        var saved = await finalDb.AdmissionCases.Include(x => x.Decisions).SingleAsync(x => x.Id == c.Id, ct);
        Assert.Equal(grant, saved.WithdrawalLetterGrantedDate); Assert.Equal(c.CreatedAtUtc.ToUnixTimeSeconds(), saved.CreatedAtUtc.ToUnixTimeSeconds()); Assert.Equal(input.Reason, Assert.Single(saved.Decisions).Notes);
        var events = await check.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents.Where(x => x.OrganizationId == org.Id && x.Action == "admission.withdrawal_letter.date_corrected").ToListAsync(ct); Assert.Single(events);
        saved.WithdrawalLetterGrantedDate = today;
        finalDb.Add(new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = "lodge_third_degree_approval", Status = "approved", AsOfDate = today, RecordedBySubject = "synthetic" }); await finalDb.SaveChangesAsync(ct);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input)).StatusCode);
    }
    private sealed class FailAudit : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (failAudit && eventData.Context!.ChangeTracker.Entries<PMGM.Api.Modules.Audit.Entities.AuditEvent>().Any(x => x.Entity.Action == "admission.withdrawal_letter.date_corrected"))
                throw new DbUpdateException("Synthetic failure", new Npgsql.PostgresException("Synthetic conflict", "ERROR", "ERROR", "40001"));
            return ValueTask.FromResult(result);
        }
    }
    private sealed class IdentityAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "Admission-CRV-Evidence-CI";
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
