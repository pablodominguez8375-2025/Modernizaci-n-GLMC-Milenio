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
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DocumentAreaCollaboratorHttpTests
{
    [Fact]
    public async Task Librarian_can_delegate_only_magazine_and_revoke_with_audit()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(cs);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        var before = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        var version = before.GetProperty("version").GetInt32();
        var subject = "comunicaciones-" + Guid.NewGuid().ToString("N");
        var payload = new
        {
            subject,
            category = "magazine",
            actions = new[] { "view", "create", "edit" },
            effectiveFrom = "2026-10-20",
            effectiveTo = "2026-12-31",
            expectedVersion = version
        };

        using var invalid = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores",
            new { subject, category = "revistas", actions = new[] { "view", "edit" },
                effectiveFrom = "2026-10-20", expectedVersion = version }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var otherArea = await client.PostAsJsonAsync(
            "/api/documentos/colaboradores", payload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, otherArea.StatusCode);
        using var created = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores", payload, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var assignmentId = json.GetProperty("assignmentId").GetGuid();
        Assert.Equal(version + 1, json.GetProperty("version").GetInt32());

        using var stale = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores", payload, ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        Assert.Contains(list.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("assignmentId").GetGuid() == assignmentId);
        var nextVersion = list.GetProperty("version").GetInt32();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            var today = new DateOnly(2026, 10, 20);
            Assert.True(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "library", "edit", "magazine", null, today));
            Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "library", "edit", "book", null, today));
            Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "documentmanager", "edit", "magazine", null, today));
            Assert.False(DynamicAccessEndpoints.IsGranted(
                catalog, subject, "library", "edit", null, today));
        }

        using var revoked = await client.DeleteAsync(
            $"/api/biblioteca/colaboradores/{assignmentId}?expectedVersion={nextVersion}", ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "library", "edit", "magazine", null,
                new DateOnly(2026, 10, 20)));
            Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(
                x => x.Action == "library.collaborator.assigned" &&
                     x.EntityId == assignmentId.ToString("D"), ct));
            Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(
                x => x.Action == "library.collaborator.revoked" &&
                     x.EntityId == assignmentId.ToString("D"), ct));
        }
    }

    [Fact]
    public async Task Archivist_may_delegate_only_document_manager_and_collaborator_may_not_delegate()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(cs);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.GrandArchivist);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/biblioteca/colaboradores", ct)).StatusCode);
        var catalog = await client.GetFromJsonAsync<JsonElement>(
            "/api/documentos/colaboradores", ct);
        var version = catalog.GetProperty("version").GetInt32();
        using var created = await client.PostAsJsonAsync(
            "/api/documentos/colaboradores",
            new
            {
                subject = "archivo-" + Guid.NewGuid().ToString("N"),
                category = "official_document",
                actions = new[] { "view" },
                effectiveFrom = "2026-10-20",
                expectedVersion = version
            }, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/documentos/colaboradores", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/documentos/colaboradores",
                new
                {
                    subject = "otro",
                    category = "official_document",
                    actions = new[] { "view" },
                    effectiveFrom = "2026-10-20",
                    expectedVersion = version + 1
                }, ct)).StatusCode);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string cs)
    {
        var root = new PmgmWebApplicationFactory(cs);
        return root.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SyntheticAuth.Name;
                options.DefaultChallengeScheme = SyntheticAuth.Name;
                options.DefaultForbidScheme = SyntheticAuth.Name;
            }).AddScheme<AuthenticationSchemeOptions, SyntheticAuth>(
                SyntheticAuth.Name, _ => { })));
    }

    private sealed class SyntheticAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "DocumentCollaboratorSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Collab-Role"].ToString();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "actor-test"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], Name));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Name)));
        }
    }
}
