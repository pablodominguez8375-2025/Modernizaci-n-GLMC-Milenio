using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AdvancementDispensationHttpTests
{
    [Fact]
    public async Task Dossier_IsPrivateAndOnlyInternalAffairsCanResolveOnce()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = DispensationTestAuthentication.SchemeName;
                options.DefaultChallengeScheme = DispensationTestAuthentication.SchemeName;
                options.DefaultForbidScheme = DispensationTestAuthentication.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, DispensationTestAuthentication>(
                DispensationTestAuthentication.SchemeName, _ => { });
        }));
        using var client = factory.CreateClient();

        var organization = new Organization { Name = "Taller CI dispensa", Type = "workshop" };
        var person = new Person { FirstNames = "Hermano", LastNames = "Dispensa CI" };
        var member = new Member { Person = person, PersonId = person.Id, CurrentDegree = "apprentice" };
        var ceremony = new CeremonyRequest
        {
            Organization = organization, OrganizationId = organization.Id,
            Member = member, MemberId = member.Id,
            CeremonyType = CeremonyCodes.Type.WageIncrease,
            Status = CeremonyCodes.RequestStatus.UnderReview
        };
        var caseRow = new AdvancementDispensationCase
        {
            CeremonyRequestId = ceremony.Id, OrganizationId = organization.Id, MemberId = member.Id,
            InstitutionalRuleId = Guid.NewGuid(), AdvancementRuleId = Guid.NewGuid(),
            SeniorityRuleId = Guid.NewGuid(), Foundation = "Caso calificado documentado para prueba HTTP.",
            MeetingId = Guid.NewGuid(), FullMinuteId = Guid.NewGuid(), BallotId = Guid.NewGuid(),
            EligibleMasterIdsJson = JsonSerializer.Serialize(new[] { Guid.NewGuid() }),
            ReductionsJson = JsonSerializer.Serialize(new[]
            {
                new AdvancementDispensationReduction(
                    AdvancementDispensationReductionPolicy.SeniorityMonths, 1)
            }),
            EligibleCount = 1, PositiveCount = 1, NegativeCount = 0,
            Status = AdvancementDispensationCaseStatuses.CouncilApproved,
            SubmittedBySubject = "secretaria-ci"
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(organization, person, member, ceremony, caseRow);
            await db.SaveChangesAsync(ct);
        }

        try
        {
            var route = $"/api/ceremonias/solicitudes/{ceremony.Id}/avance/dispensa";
            client.DefaultRequestHeaders.Add("X-Dispensation-Organization", organization.Id.ToString());
            client.DefaultRequestHeaders.Add("X-Dispensation-Role", "member");
            client.DefaultRequestHeaders.Add("X-Dispensation-Subject", "outsider-ci");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route, ct)).StatusCode);

            client.DefaultRequestHeaders.Remove("X-Dispensation-Role");
            client.DefaultRequestHeaders.Add("X-Dispensation-Role", InstitutionalRoles.TallerSecretaria);
            var read = await client.GetAsync(route, ct);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.True(read.Headers.CacheControl?.NoStore);
            var body = await read.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal(AdvancementDispensationCaseStatuses.CouncilApproved,
                body.GetProperty("status").GetString());
            Assert.False(body.TryGetProperty("eligibleMasterIds", out _));
            Assert.False(body.TryGetProperty("submittedBySubject", out _));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
                route + "/resolver", new { approved = false, resolutionReference = "RI-CI-1" }, ct)).StatusCode);

            client.DefaultRequestHeaders.Remove("X-Dispensation-Role");
            client.DefaultRequestHeaders.Remove("X-Dispensation-Subject");
            client.DefaultRequestHeaders.Add("X-Dispensation-Role", InstitutionalRoles.RegimenInterior);
            client.DefaultRequestHeaders.Add("X-Dispensation-Scope", "order");
            client.DefaultRequestHeaders.Add("X-Dispensation-Subject", "secretaria-ci");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
                route + "/resolver", new { approved = false, resolutionReference = "RI-CI-1" }, ct)).StatusCode);

            client.DefaultRequestHeaders.Remove("X-Dispensation-Subject");
            client.DefaultRequestHeaders.Add("X-Dispensation-Subject", "regimen-interior-ci");
            var resolved = await client.PostAsJsonAsync(route + "/resolver",
                new { approved = false, resolutionReference = "RI-CI-1", resolutionNotes = "No autorizar." }, ct);
            Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route + "/resolver",
                new { approved = true, resolutionReference = "RI-CI-2" }, ct)).StatusCode);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var stored = await db.AdvancementDispensationCases.AsNoTracking()
                .SingleAsync(x => x.Id == caseRow.Id, ct);
            Assert.Equal(AdvancementDispensationCaseStatuses.Rejected, stored.Status);
            Assert.Equal("regimen-interior-ci", stored.ResolvedBySubject);
            Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(x =>
                x.EntityId == caseRow.Id.ToString() &&
                x.Action == "ceremony.advancement.dispensation.resolved", ct));
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.AdvancementDispensationCases.Where(x => x.Id == caseRow.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.AuditEvents.Where(x => x.EntityId == caseRow.Id.ToString())
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CeremonyRequests.Where(x => x.Id == ceremony.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Members.Where(x => x.Id == member.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.People.Where(x => x.Id == person.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Organizations.Where(x => x.Id == organization.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
    }
}

internal sealed class DispensationTestAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dispensation-CI";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Request.Headers["X-Dispensation-Subject"].FirstOrDefault() ?? "dispensation-ci";
        var role = Request.Headers["X-Dispensation-Role"].FirstOrDefault() ?? "member";
        var claims = new List<Claim>
        {
            new("sub", subject), new(ClaimTypes.NameIdentifier, subject),
            new(InstitutionalClaims.Role, role)
        };
        var scope = Request.Headers["X-Dispensation-Scope"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(scope)) claims.Add(new(InstitutionalClaims.Scope, scope));
        var organization = Request.Headers["X-Dispensation-Organization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(organization))
            claims.Add(new(InstitutionalClaims.Organization, organization));
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
