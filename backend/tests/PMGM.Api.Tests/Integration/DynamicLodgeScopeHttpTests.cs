using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>Perfiles dinámicos con alcance «su Taller» (según la ficha) y «todos los Talleres».</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicLodgeScopeHttpTests
{
    [Fact]
    public async Task Own_lodge_follows_the_member_record_and_all_lodges_covers_every_workshop()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name;
                }).AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { })));
        var subject = "scope-" + Guid.NewGuid().ToString("N");
        Organization a = new() { Name = "Taller A", Type = "workshop" }, b = new() { Name = "Taller B", Type = "workshop" }, c = new() { Name = "Taller C", Type = "workshop" };
        Guid memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var person = new Person { FirstNames = "Hermano", LastNames = "Alcance", Rut = "QA-" + Guid.NewGuid().ToString("N")[..12] };
            var member = new Member { Person = person, PersonId = person.Id };
            var active = new Membership { Member = member, MemberId = member.Id, Organization = a, OrganizationId = a.Id, MembershipType = "regular", StartDate = new DateOnly(2020, 1, 1), Status = MembershipCodes.MembershipStatus.Active };
            var closed = new Membership { Member = member, MemberId = member.Id, Organization = b, OrganizationId = b.Id, MembershipType = "regular", StartDate = new DateOnly(2018, 1, 1), EndDate = new DateOnly(2019, 1, 1), Status = MembershipCodes.MembershipStatus.Closed };
            db.AddRange(c, person, member, active, closed);
            await db.SaveChangesAsync(ct);
            memberId = member.Id;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO core.member_identity_links (\"Id\",\"MemberId\",\"Issuer\",\"Subject\",\"CreatedAtUtc\",\"CreatedBySubject\") VALUES ({Guid.NewGuid()},{member.Id},{"urn:pmgm:unspecified-issuer"},{subject},{DateTimeOffset.UtcNow},{"ci"})", ct);
        }
        using var master = factory.CreateClient(); master.DefaultRequestHeaders.Add("X-Role", InstitutionalRoles.PlatformSuperAdmin);
        using var officer = factory.CreateClient(); officer.DefaultRequestHeaders.Add("X-Role", InstitutionalRoles.GranLogiaAdmin); officer.DefaultRequestHeaders.Add("X-Subject", subject);
        async Task<int> Version() => (await master.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct)).GetProperty("version").GetInt32();
        async Task<bool> Allowed(Guid org) => (await officer.GetFromJsonAsync<JsonElement>($"/api/system/access/evaluate?viewCode=calendar&action=view&organizationId={org}", ct)).GetProperty("allowed").GetBoolean();

        var ownCode = "own-" + Guid.NewGuid().ToString("N")[..8]; var allCode = "all-" + Guid.NewGuid().ToString("N")[..8];
        var v = await Version();
        foreach (var (code, scope) in new[] { (ownCode, "own-lodge"), (allCode, "all-lodges") })
        {
            Assert.Equal(HttpStatusCode.OK, (await master.PostAsJsonAsync("/api/system/access/profiles", new { code, name = code, scope, menuCodes = new[] { "personal" }, expectedVersion = v++ }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await master.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants", new { grants = new[] { new { viewCode = "calendar", actions = new[] { "view" } } }, expectedVersion = v++ }, ct)).StatusCode);
        }
        // El Taller no se elige: un perfil derivado rechaza un Taller expreso.
        Assert.Equal(HttpStatusCode.BadRequest, (await master.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = ownCode, organizationId = a.Id, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = v }, ct)).StatusCode);
        var assigned = await master.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = ownCode, organizationId = (Guid?)null, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = v++ }, ct);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignmentId = (await assigned.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("assignments").EnumerateArray().Single(x => x.GetProperty("profileCode").GetString() == ownCode).GetProperty("id").GetGuid();

        // Su Taller = pertenencia vigente de la ficha (A); la cerrada (B) y los ajenos (C) no cuentan.
        Assert.True(await Allowed(a.Id)); Assert.False(await Allowed(b.Id)); Assert.False(await Allowed(c.Id));
        // El catálogo guardado nunca recibe las asignaciones derivadas.
        var stored = await master.GetFromJsonAsync<JsonElement>("/api/system/access/catalog", ct);
        Assert.Single(stored.GetProperty("assignments").EnumerateArray(), x => x.GetProperty("subject").GetString() == subject);

        // Si la ficha cambia, el acceso se mueve solo: cierra A y abre C.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var open = await db.Memberships.FirstAsync(x => x.MemberId == memberId && x.OrganizationId == a.Id, ct);
            open.Status = MembershipCodes.MembershipStatus.Closed; open.EndDate = new DateOnly(2026, 1, 1);
            db.Memberships.Add(new Membership { MemberId = open.MemberId, OrganizationId = c.Id, MembershipType = "regular", StartDate = new DateOnly(2026, 1, 2), Status = MembershipCodes.MembershipStatus.Active });
            await db.SaveChangesAsync(ct);
        }
        Assert.False(await Allowed(a.Id)); Assert.True(await Allowed(c.Id));

        // Todos los Talleres: cubre A, B y C, sin elegir ninguno.
        Assert.Equal(HttpStatusCode.OK, (await master.DeleteAsync($"/api/system/access/assignments/{assignmentId}?expectedVersion={v}", ct)).StatusCode); v++;
        Assert.Equal(HttpStatusCode.OK, (await master.PostAsJsonAsync("/api/system/access/assignments", new { subject, profileCode = allCode, organizationId = (Guid?)null, effectiveFrom = "2020-01-01", effectiveTo = (string?)null, expectedVersion = v }, ct)).StatusCode);
        Assert.True(await Allowed(a.Id)); Assert.True(await Allowed(b.Id)); Assert.True(await Allowed(c.Id));
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "DynamicLodgeScopeSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Role"].FirstOrDefault() ?? "reader";
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", Request.Headers["X-Subject"].FirstOrDefault() ?? role),
                new Claim(InstitutionalClaims.Scope, "order"), new Claim(InstitutionalClaims.Role, role)
            ], Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
