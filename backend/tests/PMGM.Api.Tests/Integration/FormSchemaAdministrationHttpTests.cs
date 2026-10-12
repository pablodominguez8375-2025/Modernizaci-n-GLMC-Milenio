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
using PMGM.Api.Modules.FormConfiguration;
using PMGM.Api.Tests.Unit;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class FormSchemaAdministrationHttpTests
{
    private const string BaseRoute = "/api/system/forms/candidate_profile/versions";
    private static readonly string? Connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string connection)
    {
        var root = new PmgmWebApplicationFactory(connection);
        return root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = FormAuth.Name;
                options.DefaultChallengeScheme = FormAuth.Name;
                options.DefaultForbidScheme = FormAuth.Name;
            }).AddScheme<AuthenticationSchemeOptions, FormAuth>(FormAuth.Name, _ => { })));
    }

    private static object PublishPayload(int version, int expectedPreviousVersion, DateTimeOffset effective) =>
        new
        {
            version, expectedPreviousVersion, effectiveFromUtc = effective,
            fields = FormSchemaInstitutionalCatalogTests.ValidFields()
        };

    [Fact]
    public async Task AdminCanPublishAndReadExactScopedHistoryWithAuditAndOptimisticConflict()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection);
        using var client = factory.CreateClient();

        var lodgeA = new Organization { Name = "Taller formularios HTTP A", Type = "workshop" };
        var lodgeB = new Organization { Name = "Taller formularios HTTP B", Type = "workshop" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Organizations.AddRange(lodgeA, lodgeB);
            await db.SaveChangesAsync(ct);
        }

        var catalog = await client.GetAsync("/api/system/forms/catalog", ct);
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        Assert.True(catalog.Headers.CacheControl?.NoStore);
        var catalogData = await catalog.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Contains("candidate_id", catalogData.GetProperty("forms")[0]
            .GetProperty("trustedCoreKeys").EnumerateArray().Select(x => x.GetString()));

        var scoped = BaseRoute + "?organizationId=" + lodgeA.Id;
        var firstDate = DateTimeOffset.UtcNow.AddDays(1);
        var invalid = await client.PostAsJsonAsync(scoped, new
        {
            version = 1, expectedPreviousVersion = 0, effectiveFromUtc = firstDate,
            fields = new[] { new FormFieldDefinition("custom_note", "Nota", "text") }
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var created = await client.PostAsJsonAsync(scoped, PublishPayload(1, 0, firstDate), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var metadata = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, metadata.GetProperty("version").GetInt32());
        Assert.Equal(64, metadata.GetProperty("payloadSha256").GetString()?.Length);

        var old = await client.GetAsync(BaseRoute + "/1?organizationId=" + lodgeA.Id, ct);
        Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        Assert.True(old.Headers.CacheControl?.NoStore);
        var oldSchema = await old.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, oldSchema.GetProperty("version").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync(BaseRoute + "/1?organizationId=" + lodgeB.Id, ct)).StatusCode);
        var replay = await client.PostAsJsonAsync(scoped, PublishPayload(1, 0, firstDate), ct);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);

        var second = await client.PostAsJsonAsync(scoped,
            PublishPayload(2, 1, firstDate.AddDays(1)), ct);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(scoped, ct);
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>(
            BaseRoute + "/1?organizationId=" + lodgeA.Id, ct)).GetProperty("version").GetInt32());

        await using var verification = factory.Services.CreateAsyncScope();
        var persisted = verification.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var scopeKey = FormSchemaScope.ForOrganization(lodgeA.Id);
        Assert.Equal(2, await persisted.FormSchemaVersions.AsNoTracking()
            .CountAsync(x => x.FormCode == FormSchemaInstitutionalCatalog.CandidateProfile &&
                x.ScopeKey == scopeKey, ct));
        Assert.Equal(2, await persisted.AuditEvents.AsNoTracking()
            .CountAsync(x => x.OrganizationId == lodgeA.Id &&
                x.Action == "system.forms.schema_version_published", ct));
    }

    [Fact]
    public async Task NonAdminAndAnonymousCannotReadOrMutatePrivateSchemas()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection);
        using var client = factory.CreateClient();
        await using (var databaseScope = factory.Services.CreateAsyncScope())
            await databaseScope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);

        client.DefaultRequestHeaders.Add("X-Form-Role", InstitutionalRoles.TallerSecretaria);
        client.DefaultRequestHeaders.Add("X-Form-Scope", "lodge");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/system/forms/catalog", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync(BaseRoute, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(BaseRoute,
                PublishPayload(1, 0, DateTimeOffset.UtcNow.AddDays(1)), ct)).StatusCode);

        using var noSubject = factory.CreateClient();
        noSubject.DefaultRequestHeaders.Add("X-Form-No-Subject", "1");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await noSubject.PostAsJsonAsync(BaseRoute,
                PublishPayload(1, 0, DateTimeOffset.UtcNow.AddDays(1)), ct)).StatusCode);

        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Form-Anonymous", "1");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/system/forms/catalog", ct)).StatusCode);
    }

    [Fact]
    public async Task RejectsUnknownScopeBackdatingAndPrivacyDowngrades()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection);
        using var client = factory.CreateClient();
        await using (var databaseScope = factory.Services.CreateAsyncScope())
            await databaseScope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
        var unknown = BaseRoute + "?organizationId=" + Guid.NewGuid();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(unknown,
                PublishPayload(1, 0, DateTimeOffset.UtcNow.AddDays(1)), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(BaseRoute,
                PublishPayload(1, 0, DateTimeOffset.UtcNow.AddDays(-1)), ct)).StatusCode);

        var fields = FormSchemaInstitutionalCatalogTests.ValidFields()
            .Concat([new FormFieldDefinition("photo", "Fotografía", "photo",
                Classification: FormFieldClassification.Public)]).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(BaseRoute, new
            {
                version = 1, expectedPreviousVersion = 0,
                effectiveFromUtc = DateTimeOffset.UtcNow.AddDays(1), fields
            }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/system/forms/unknown_form/versions", ct)).StatusCode);
    }

    private sealed class FormAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "FormsAdmin-CI";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Form-Anonymous"))
                return Task.FromResult(AuthenticateResult.NoResult());

            var role = Request.Headers["X-Form-Role"].FirstOrDefault()
                ?? InstitutionalRoles.GranLogiaAdmin;
            var scope = Request.Headers["X-Form-Scope"].FirstOrDefault() ?? "order";
            var claims = new List<Claim>
            {
                new(InstitutionalClaims.Role, role),
                new(InstitutionalClaims.Scope, scope)
            };
            if (!Request.Headers.ContainsKey("X-Form-No-Subject"))
                claims.Add(new Claim("sub", "forms-admin-ci"));
            var identity = new ClaimsIdentity(claims, Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
