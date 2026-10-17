using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class SpaceRequestHttpWorkflowTests
{
    [Fact]
    public async Task Two_lodges_are_isolated_and_grand_secretariat_alone_creates_reservation()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        using var factory = new SpaceRequestTestFactory(connectionString);
        var ct = TestContext.Current.CancellationToken;
        var lodgeA = Guid.NewGuid();
        var lodgeB = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var ceremonyId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Organizations.AddRange(
                new Organization { Id = lodgeA, Name = "Taller QA A", Number = "QA-A", Type = "workshop" },
                new Organization { Id = lodgeB, Name = "Taller QA B", Number = "QA-B", Type = "workshop" });
            db.CeremonyRequests.Add(new CeremonyRequest
            {
                Id = ceremonyId, OrganizationId = lodgeA,
                CeremonyType = CeremonyCodes.Type.WageIncrease,
                ProposedDate = new DateOnly(2035, 12, 5),
                Status = CeremonyCodes.RequestStatus.Authorized
            });
            await db.SaveChangesAsync(ct);
            var gs = scope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
            gs.InstitutionalSpaces.Add(new InstitutionalSpace
            {
                Id = spaceId, Code = "QA-ROOM-" + Guid.NewGuid().ToString("N")[..8],
                Name = "Sala de test aislada", SpaceType = "secretariat_room", Capacity = 40,
                Status = "active"
            });
            await gs.SaveChangesAsync(ct);
        }

        using var a = Client(factory, InstitutionalRoles.TallerSecretaria, lodgeA);
        using var b = Client(factory, InstitutionalRoles.TallerSecretaria, lodgeB);
        using var grand = Client(factory, InstitutionalRoles.GranSecretaria, null);
        using var treasurer = Client(factory, InstitutionalRoles.TallerTesoreria, lodgeA);
        var start = new DateTimeOffset(2035, 11, 1, 19, 0, 0, TimeSpan.Zero);
        object body = new
        {
            spaceType = "secretariat_room", preferredSpaceId = spaceId,
            startsAtUtc = start, endsAtUtc = start.AddHours(3), purpose = "tenida",
            notes = "Actividad ficticia", expectedAttendees = 25,
            ceremonyRequestId = (Guid?)null, equipmentRequest = (string?)null,
            equipmentResponsible = (string?)null, vmApprovalReference = (string?)null
        };

        var unauthorized = await treasurer.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", body, ct);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);

        var created = await a.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", body, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = createdJson.GetProperty("id").GetGuid();
        Assert.Equal(lodgeA, createdJson.GetProperty("organizationId").GetGuid());
        Assert.Equal("pending", createdJson.GetProperty("status").GetString());

        var bList = await b.GetAsync("/api/secretaria/solicitudes-espacio", ct);
        Assert.Equal(HttpStatusCode.OK, bList.StatusCode);
        var bRows = await bList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(0, bRows.GetProperty("total").GetInt32());

        var bCancel = await b.PostAsync("/api/secretaria/solicitudes-espacio/" + requestId + "/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.NotFound, bCancel.StatusCode);

        var aList = await a.GetAsync("/api/secretaria/solicitudes-espacio", ct);
        var aRows = await aList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, aRows.GetProperty("total").GetInt32());

        var before = await a.GetAsync("/api/secretaria/espacios/disponibilidad?fromUtc=2035-11-01T19:00:00Z&toUtc=2035-11-01T22:00:00Z", ct);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeJson = await before.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.True(beforeJson.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == spaceId).GetProperty("isAvailable").GetBoolean());

        var grandRows = await grand.GetAsync("/api/gran-secretaria/solicitudes-espacio?status=pending", ct);
        Assert.Equal(HttpStatusCode.OK, grandRows.StatusCode);
        var grandJson = await grandRows.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Contains(grandJson.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == requestId);

        var approved = await grand.PostAsJsonAsync("/api/gran-secretaria/solicitudes-espacio/" + requestId + "/aprobar",
            new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedJson = await approved.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("approved", approvedJson.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, approvedJson.GetProperty("spaceReservationId").GetGuid());

        var duplicate = await grand.PostAsJsonAsync("/api/gran-secretaria/solicitudes-espacio/" + requestId + "/aprobar",
            new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var conflicting = await b.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", body, ct);
        Assert.Equal(HttpStatusCode.Created, conflicting.StatusCode);
        var conflictingId = (await conflicting.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("id").GetGuid();
        var conflictResult = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + conflictingId + "/aprobar", new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, conflictResult.StatusCode);
        var bAfter = await b.GetAsync("/api/secretaria/solicitudes-espacio", ct);
        var bAfterJson = await bAfter.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Contains(bAfterJson.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == conflictingId &&
                 x.GetProperty("status").GetString() == "pending");

        var after = await a.GetAsync("/api/secretaria/espacios/disponibilidad?fromUtc=2035-11-01T19:00:00Z&toUtc=2035-11-01T22:00:00Z", ct);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var serialized = await after.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("Taller QA B", serialized);
        Assert.DoesNotContain("Actividad ficticia", serialized);
        Assert.False((await after.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == spaceId)
            .GetProperty("isAvailable").GetBoolean());

        var rejected = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + conflictingId + "/rechazar",
            new { reason = "Horario ocupado; buscar nueva fecha" }, ct);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedJson = await rejected.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("rejected", rejectedJson.GetProperty("status").GetString());

        var ceremonyBody = new
        {
            spaceType = "secretariat_room", preferredSpaceId = spaceId,
            startsAtUtc = start.AddMonths(1), endsAtUtc = start.AddMonths(1).AddHours(2),
            purpose = "ceremonia", ceremonyRequestId = ceremonyId,
            notes = "Solicitud ficticia sin Plancha",
            expectedAttendees = 20,
            equipmentRequest = (string?)null, equipmentResponsible = (string?)null,
            vmApprovalReference = (string?)null
        };
        var ceremonyCreated = await a.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", ceremonyBody, ct);
        Assert.Equal(HttpStatusCode.Created, ceremonyCreated.StatusCode);
        var ceremonySpaceId = (await ceremonyCreated.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("id").GetGuid();
        var blockedWithoutPlancha = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + ceremonySpaceId + "/aprobar",
            new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, blockedWithoutPlancha.StatusCode);

        var cancellation = await a.PostAsync(
            "/api/secretaria/solicitudes-espacio/" + ceremonySpaceId + "/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);
        Assert.Equal("cancelled",
            (await cancellation.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
                .GetProperty("status").GetString());

        await using var checkScope = factory.Services.CreateAsyncScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
        Assert.Equal(1, await checkDb.SpaceReservations.AsNoTracking()
            .CountAsync(x => x.SpaceId == spaceId && x.Status == "reserved", ct));
        Assert.True(await checkDb.AuditEvents.AsNoTracking().AnyAsync(x =>
            x.Action == "grand_secretariat.space_request.approved" &&
            x.EntityId == requestId.ToString(), ct));
    }

    private static HttpClient Client(SpaceRequestTestFactory factory, string role, Guid? organizationId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Synthetic-Role", role);
        if (organizationId is not null)
            client.DefaultRequestHeaders.Add("X-Synthetic-Lodge", organizationId.Value.ToString());
        return client;
    }
}

internal sealed class SpaceRequestTestFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.RemoveAll<GrandSecretariatDbContext>();
            services.RemoveAll<DbContextOptions<GrandSecretariatDbContext>>();
            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<GrandSecretariatDbContext>(options => options.UseNpgsql(connectionString));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SyntheticHeaderAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = SyntheticHeaderAuthenticationHandler.Scheme;
                options.DefaultForbidScheme = SyntheticHeaderAuthenticationHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, SyntheticHeaderAuthenticationHandler>(
                SyntheticHeaderAuthenticationHandler.Scheme, _ => { });
        });
    }
}

/// <summary>Only in integration test assembly. Never use these headers in production authentication.</summary>
public sealed class SyntheticHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "SyntheticSpaceRequestTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Synthetic-Role"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(InstitutionalClaims.Role, role),
            new("sub", "synthetic-qa-" + role) };
        var org = Request.Headers["X-Synthetic-Lodge"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(org))
            claims.Add(new Claim(InstitutionalClaims.Organization, org));
        if (role == InstitutionalRoles.GranSecretaria)
            claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        var identity = new ClaimsIdentity(claims, Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
