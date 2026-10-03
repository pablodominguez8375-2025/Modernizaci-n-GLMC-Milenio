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
public sealed class AdmissionWithdrawalEvidenceHttpTests
{
    [Fact]
    public async Task Signature_is_bound_to_latest_reviewed_letter_and_cannot_be_reused()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AdmissionsDbContext>(); services.RemoveAll<DbContextOptions<AdmissionsDbContext>>();
            services.AddDbContext<AdmissionsDbContext>(options => options.UseNpgsql(connection));
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = IdentityAuth.Name; options.DefaultChallengeScheme = IdentityAuth.Name; options.DefaultForbidScheme = IdentityAuth.Name; }).AddScheme<AuthenticationSchemeOptions, IdentityAuth>(IdentityAuth.Name, _ => { });
        }));
        using var client = factory.CreateClient();
        var today = AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow);
        var org = new Organization { Name = "CRV evidence synthetic", Type = "workshop" };
        var c = new AdmissionCase { OrganizationId = org.Id, PersonId = Guid.NewGuid(), AdmissionType = "affiliation", AffiliationMode = "simple", WithdrawalLetterGrantedDate = today.AddDays(-1), Status = "under_review", CreatedBySubject = "synthetic" };
        var letter = new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", DocumentVersionId = Guid.NewGuid(), EvidenceDate = c.WithdrawalLetterGrantedDate, ReviewStatus = "pending", CreatedBySubject = "synthetic", CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2) };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await core.Database.MigrateAsync(ct); core.Add(org); await core.SaveChangesAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); await db.Database.MigrateAsync(ct); db.Add(c); db.Add(letter); await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-CI-Organization", org.Id.ToString());
        var input = new WithdrawalLetterSignatureReviewRequest("approved", today, "SYNTHETIC ORIGINAL REVIEW", null, letter.Id);
        var route = $"/api/admisiones/expedientes/{c.Id}/verificaciones/carta-retiro-firma-manuscrita";
        async Task<HttpResponseMessage> Post(WithdrawalLetterSignatureReviewRequest value) => await client.PostAsJsonAsync(route, value, ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(input)).StatusCode);
        client.DefaultRequestHeaders.Add("X-CI-Role", InstitutionalRoles.GranSecretaria); client.DefaultRequestHeaders.Add("X-CI-Scope", "order");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(input with { EvidenceId = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(input with { AsOfDate = today.AddDays(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(input with { SourceReference = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input with { EvidenceId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); var e = await db.AdmissionEvidence.SingleAsync(x => x.Id == letter.Id, ct); e.ReviewStatus = "approved"; e.ReviewedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(ct);
        }
        var accepted = await Post(input); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var json = await accepted.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct); Assert.Equal(AdmissionWorkflowCodes.DecisionType.WithdrawalSignature(letter.Id), json.GetProperty("decisionType").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); var saved = await db.AdmissionCases.Include(x => x.Evidence).Include(x => x.Decisions).SingleAsync(x => x.Id == c.Id, ct); Assert.NotNull(AdmissionWithdrawalEvidencePolicy.VerifiedSignature(saved, today));
            db.Add(new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", DocumentVersionId = Guid.NewGuid(), EvidenceDate = today, ReviewStatus = "rejected", CreatedBySubject = "synthetic" }); await db.SaveChangesAsync(ct);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input)).StatusCode);
        foreach (var suffix in new[] { "elegibilidad", "habilitacion-procedimiento" })
        {
            var response = await client.GetAsync($"/api/admisiones/expedientes/{c.Id}/{suffix}", ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var value = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var requirements = suffix == "elegibilidad" ? value.GetProperty("eligibility").GetProperty("requirements") : value.GetProperty("requirements");
            Assert.Contains(requirements.EnumerateArray(), x => x.GetProperty("code").GetString() == AdmissionCodes.Requirement.WithdrawalLetterHandwrittenSignature && x.GetProperty("status").GetString() != "approved");
        }
        await using var check = factory.Services.CreateAsyncScope(); var finalDb = check.ServiceProvider.GetRequiredService<AdmissionsDbContext>();
        Assert.Equal(1, await finalDb.AdmissionDecisions.CountAsync(x => x.AdmissionCaseId == c.Id, ct));
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents.CountAsync(x => x.OrganizationId == org.Id && x.Action == "admission.withdrawal_letter.handwritten_signature_verified", ct));
        var closed = await finalDb.AdmissionCases.SingleAsync(x => x.Id == c.Id, ct); closed.Status = "resolved"; await finalDb.SaveChangesAsync(ct);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(input)).StatusCode);
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
