using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>#1035: endpoints reales de Sistema › Talleres contra PostgreSQL.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class WorkshopRegistryHttpTests
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string connection, bool retireEnabled = true)
        => new PmgmWebApplicationFactory(connection).WithWebHostBuilder(b => b.UseSetting("WorkshopRegistry:RetireEnabled", retireEnabled ? "true" : "false").ConfigureTestServices(s => s
            .AddAuthentication(o => { o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name; })
            .AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { })));

    private static HttpClient As(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string kind)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workshop-Actor", kind);
        return client;
    }

    private static async Task<JsonElement> Registry(HttpClient client, CancellationToken ct) => await client.GetFromJsonAsync<JsonElement>("/api/system/workshops", ct);
    private static JsonElement Item(JsonElement registry, Guid id) => registry.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task Master_lists_deletes_empty_retires_with_data_reactivates_and_audits()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var parent = new Organization { Name = $"Taller con datos {suffix}", Number = "9" + Random.Shared.Next(10000, 99999), Type = "workshop", Origin = "initial_load" };
        var empty = new Organization { Name = $"Taller vacío {suffix}", Type = "workshop", Origin = "qa_seed" };
        // Taller 00 de soporte: lo crea la migración #812; no se insertan Gran Logias para no alterar otras pruebas.
        var support00 = Guid.Parse("b12c0000-0000-4000-8000-000000000000");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            // Runbook de T1: tras migrar TODOS los modulos se reinstala la guardia para cubrir las tablas de otros contextos.
            // Sin esto la baja se rechaza a proposito (409 GUARD_INCOMPLETE, fail-closed).
            // GATE de release (#1039): las columnas de organizacion que otros modulos crean DESPUES de T1 y aun no tienen guardia
            // son exactamente las conocidas. Un modulo nuevo con columna de organizacion rompe esta prueba hasta que alguien lo
            // revise y lo agregue aqui (o llame al instalador en su propia migracion): nunca queda sin cubrir en silencio.
            var known = new HashSet<string>
            {
                "core.admission_cases.OrganizationId", "core.admission_cases.OriginOrganizationId",
                "core.assembly_debt_decision_events.OrganizationId", "core.assembly_member_statuses.OrganizationId",
                "core.assembly_treasury_certification_events.OrganizationId", "core.assembly_treasury_signed_reviews.OrganizationId",
                "core.assembly_workshop_balance_events.OrganizationId", "core.calendar_events.OrganizationId",
                "core.candidate_intake_profiles.OrganizationId", "core.data_quality_cases.OrganizationId",
            };
            var unknownUncovered = (await PMGM.Api.Modules.SystemConfiguration.WorkshopRegistryEndpoints.GuardCoverageMissingAsync(db, ct))
                .Where(x => !known.Contains(x)).ToList();
            Assert.True(unknownUncovered.Count == 0, "Columnas de organizacion nuevas sin guardia ni revision: " + string.Join(", ", unknownUncovered));
            await db.Database.ExecuteSqlRawAsync("SELECT core.install_workshop_retired_guard()", ct);
            Assert.Empty(await PMGM.Api.Modules.SystemConfiguration.WorkshopRegistryEndpoints.GuardCoverageMissingAsync(db, ct));
            db.Organizations.AddRange(parent, empty);
            await db.SaveChangesAsync(ct);
            db.Organizations.Add(new Organization { Name = $"Hijo {suffix}", Type = "workshop", ParentOrganizationId = parent.Id });
            await db.SaveChangesAsync(ct);
        }

        using var master = As(factory, "master");
        var registry = await Registry(master, ct);
        Assert.True(registry.GetProperty("canManage").GetBoolean());
        Assert.Equal(10, registry.GetProperty("reasonMinLength").GetInt32());
        Assert.Equal(500, registry.GetProperty("reasonMaxLength").GetInt32());
        Assert.True(Item(registry, parent.Id).GetProperty("hasData").GetBoolean());
        Assert.False(Item(registry, empty.Id).GetProperty("hasData").GetBoolean());
        Assert.Equal("qa_seed", Item(registry, empty.Id).GetProperty("origin").GetString());
        Assert.True(Item(registry, support00).GetProperty("protected").GetBoolean());
        Assert.Equal(empty.Id.ToString("N")[..8].ToUpperInvariant(), Item(registry, empty.Id).GetProperty("shortCode").GetString());

        var code = empty.Id.ToString("N")[..8].ToUpperInvariant();
        var wrong = await master.PostAsJsonAsync($"/api/system/workshops/{empty.Id}/delete", new { reason = "Prueba de eliminación", confirmation = "otro" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var shortReason = await master.PostAsJsonAsync($"/api/system/workshops/{empty.Id}/delete", new { reason = "corto", confirmation = code }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, shortReason.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await master.PostAsJsonAsync($"/api/system/workshops/{parent.Id}/delete", new { reason = "Intento con datos", confirmation = parent.Number }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await master.PostAsJsonAsync($"/api/system/workshops/{support00}/retire", new { reason = "Intento protegido", confirmation = "00" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await master.PostAsJsonAsync($"/api/system/workshops/{Guid.NewGuid()}/retire", new { reason = "Taller inexistente", confirmation = "x" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await master.PostAsJsonAsync($"/api/system/workshops/{empty.Id}/purge", new { reason = "Acción desconocida", confirmation = code }, ct)).StatusCode);

        var deleted = await master.PostAsJsonAsync($"/api/system/workshops/{empty.Id}/delete", new { reason = "Taller de prueba sin datos", confirmation = code }, ct);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.DoesNotContain((await Registry(master, ct)).GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == empty.Id);

        var retire = await master.PostAsJsonAsync($"/api/system/workshops/{parent.Id}/retire", new { reason = "Taller en receso indefinido", confirmation = parent.Number }, ct);
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);
        var retired = Item(await Registry(master, ct), parent.Id);
        Assert.Equal("retired", retired.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, retired.GetProperty("retiredAtUtc").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await master.PostAsJsonAsync($"/api/system/workshops/{parent.Id}/retire", new { reason = "Segunda baja no válida", confirmation = parent.Number }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await master.PostAsJsonAsync($"/api/system/workshops/{parent.Id}/reactivate", new { reason = "Retoma sus trabajos" }, ct)).StatusCode);
        Assert.Equal("active", Item(await Registry(master, ct), parent.Id).GetProperty("status").GetString());

        var csv = await master.GetAsync("/api/system/workshops/export.csv", ct);
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType?.ToString());
        Assert.Equal("talleres-registrados.csv", csv.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await csv.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Contains(parent.Id.ToString(), Encoding.UTF8.GetString(bytes));

        await using var check = factory.Services.CreateAsyncScope();
        var audit = check.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents.AsNoTracking();
        Assert.True(await audit.AnyAsync(a => a.Action == "system.workshops.delete" && a.EntityId == empty.Id.ToString(), ct));
        Assert.True(await audit.AnyAsync(a => a.Action == "system.workshops.retire" && a.EntityId == parent.Id.ToString(), ct));
        Assert.True(await audit.AnyAsync(a => a.Action == "system.workshops.reactivate" && a.EntityId == parent.Id.ToString(), ct));
        Assert.True(await audit.AnyAsync(a => a.Action == "system.workshops.delete_rejected" && a.EntityId == parent.Id.ToString(), ct));
        Assert.True(await audit.AnyAsync(a => a.Action == "system.workshops.exported", ct));
    }

    [Fact]
    public async Task Superuser_views_and_downloads_but_cannot_mutate_and_others_are_denied()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection);
        var org = new Organization { Name = "Taller superusuario " + Guid.NewGuid().ToString("N")[..6], Type = "workshop" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Organizations.Add(org);
            await db.SaveChangesAsync(ct);
        }

        using var superuser = As(factory, "superuser");
        var registry = await Registry(superuser, ct);
        Assert.False(registry.GetProperty("canManage").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await superuser.GetAsync("/api/system/workshops/export.csv", ct)).StatusCode);
        foreach (var action in new[] { "delete", "retire" })
            Assert.Equal(HttpStatusCode.Forbidden, (await superuser.PostAsJsonAsync($"/api/system/workshops/{org.Id}/{action}", new { reason = "Intento sin permiso", confirmation = org.Id.ToString("N")[..8].ToUpperInvariant() }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await superuser.PostAsJsonAsync($"/api/system/workshops/{org.Id}/reactivate", new { reason = "Intento sin permiso" }, ct)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Organizations.AnyAsync(x => x.Id == org.Id && x.Status == "active", ct));

        using var treasurer = As(factory, "treasurer");
        Assert.Equal(HttpStatusCode.Forbidden, (await treasurer.GetAsync("/api/system/workshops", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await treasurer.GetAsync("/api/system/workshops/export.csv", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await treasurer.PostAsJsonAsync($"/api/system/workshops/{org.Id}/retire", new { reason = "Intento sin permiso" }, ct)).StatusCode);

        using var anonymous = As(factory, "anonymous");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/system/workshops", ct)).StatusCode);
    }

    [Fact]
    public async Task Retire_is_blocked_by_default_and_the_registry_says_so()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection, retireEnabled: false);
        var org = new Organization { Name = "Taller baja bloqueada " + Guid.NewGuid().ToString("N")[..6], Type = "workshop" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Organizations.Add(org);
            await db.SaveChangesAsync(ct);
        }
        using var master = As(factory, "master");
        Assert.False((await Registry(master, ct)).GetProperty("retireEnabled").GetBoolean());
        var response = await master.PostAsJsonAsync($"/api/system/workshops/{org.Id}/retire", new { reason = "Intento con baja bloqueada", confirmation = org.Id.ToString("N")[..8].ToUpperInvariant() }, ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("RetireNotEnabled", (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString());
        await using var check = factory.Services.CreateAsyncScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<PmgmDbContext>().Organizations.AnyAsync(x => x.Id == org.Id && x.Status == "active", ct));
    }

    [Fact]
    public async Task Technical_origin_is_derived_and_export_audit_does_not_record_counts()
    {
        if (string.IsNullOrWhiteSpace(Connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(Connection);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        using var master = As(factory, "master");
        var support = Item(await Registry(master, ct), Guid.Parse("b12c0000-0000-4000-8000-000000000000"));
        Assert.Equal("bootstrap", support.GetProperty("origin").GetString());
        Assert.True(support.GetProperty("protected").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await master.GetAsync("/api/system/workshops/export.csv", ct)).StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var last = await db.AuditEvents.AsNoTracking().Where(a => a.Action == "system.workshops.exported").OrderByDescending(a => a.OccurredAtUtc).FirstAsync(ct);
        Assert.DoesNotContain("rows", last.MetadataJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "WorkshopRegistry-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var kind = Request.Headers["X-Workshop-Actor"].FirstOrDefault();
            if (kind is null or "anonymous") return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", "workshop-ci-" + kind), new(InstitutionalClaims.Scope, "order") };
            switch (kind)
            {
                case "master": claims.Add(new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin)); break;
                case "superuser": claims.Add(new Claim(InstitutionalClaims.AccountKind, "superuser")); break;
                default: claims.Add(new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerTesoreria)); break;
            }
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
