using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyPreliminaryReviewHttpTests
{
    [Fact]
    public async Task Verified_internal_affairs_can_read_provisional_review_without_personal_labels()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                // La fábrica general reconfigura PmgmDbContext, pero
                // RegimenInteriorDbContext debe apuntar al PostgreSQL sintético.
                s.RemoveAll<RegimenInteriorDbContext>();
                s.RemoveAll<DbContextOptions<RegimenInteriorDbContext>>();
                s.AddDbContext<RegimenInteriorDbContext>(o => o.UseNpgsql(cs));
                s.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = InternalAffairsTestAuth.SchemeName;
                    o.DefaultChallengeScheme = InternalAffairsTestAuth.SchemeName;
                    o.DefaultForbidScheme = InternalAffairsTestAuth.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, InternalAffairsTestAuth>(
                    InternalAffairsTestAuth.SchemeName, _ => {});
            }));

        var assembly = new AssemblyRecord
        {
            Name = "Asamblea de ensayo",
            MeetingDate = new DateOnly(2026, 11, 19),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "qa-interno"
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Assemblies.Add(assembly);
            var member = new AssemblyMemberStatusRecord
            {
                AssemblyId = assembly.Id,
                MemberId = Guid.NewGuid(), PersonId = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                Category = "representative",
                SourceOfficeAssignmentId = Guid.NewGuid(),
                SourceEvidenceReference = "VM-TEST",
                FirstNamesAtCutoff = "Nombre",
                LastNamesAtCutoff = "Apellido",
                RutAtCutoff = "00000000-0",
                WorkshopAtCutoff = "Taller sintético",
                VerifiedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            };
            db.AssemblyMemberStatuses.Add(member);
            db.AssemblyEligibilities.Add(new AssemblyEligibilityRecord
            {
                AssemblyId = assembly.Id, MemberStatusId = member.Id,
                Status = "pendiente", MayAttend = false, MayVote = false,
                ReasonCode = "EVIDENCIA_PENDIENTE",
                EvaluatedBySubject = "qa-interno"
            });
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/regimen-interior/asambleas/{assembly.Id}/revision", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("provisional", text);
        Assert.DoesNotContain("00000000-0", text);
        Assert.DoesNotContain("Taller sintético", text);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/regimen-interior/asambleas/{Guid.NewGuid()}/revision", ct)).StatusCode);
    }

    private sealed class InternalAffairsTestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AssemblyReadSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "ri-test"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior)
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
