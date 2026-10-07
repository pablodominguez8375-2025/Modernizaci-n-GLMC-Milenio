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
using PMGM.Api.Modules.Treasury.Entities;
using PMGM.Api.Modules.Ceremonies.Entities;
using Xunit;
namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicGrandTreasuryAccessHttpTests
{
    [Fact]
    public async Task Order_view_write_revocation_and_institutional_authority_are_enforced_without_hidden_mutation()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var ct=TestContext.Current.CancellationToken;
        using var root=new PmgmWebApplicationFactory(connection);
        using var factory=root.WithWebHostBuilder(b=>b.ConfigureTestServices(s=>s.AddAuthentication(o=>
        {o.DefaultAuthenticateScheme=Auth.Name;o.DefaultChallengeScheme=Auth.Name;o.DefaultForbidScheme=Auth.Name;})
            .AddScheme<AuthenticationSchemeOptions,Auth>(Auth.Name,_=>{})));
        using var admin=factory.CreateClient(); using var client=factory.CreateClient();
        var org=new Organization{Name="Gran Tesorería permisos QA",Type="workshop"};
        var statement=new TreasuryMonthlyStatement{OrganizationId=org.Id,PeriodYear=2026,PeriodMonth=10,CutoffDate=new DateOnly(2026,10,31),Status="submitted"};
        var ceremony=new CeremonyRequest{OrganizationId=org.Id,CeremonyType="initiation",Status="requested"};
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();await db.Database.MigrateAsync(ct);
            db.Organizations.Add(org);db.TreasuryMonthlyStatements.Add(statement);db.CeremonyRequests.Add(ceremony);await db.SaveChangesAsync(ct);
        }
        var subject="grand-treasury-"+Guid.NewGuid().ToString("N");var code="qa-"+Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-Treasury-Subject",subject);
        var version=(await admin.GetFromJsonAsync<JsonElement>("/api/system/access/catalog",ct)).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.OK,(await admin.PostAsJsonAsync("/api/system/access/profiles",new{code,name="Consulta Tesorería QA",scope="order",menuCodes=new[]{"treasury"},expectedVersion=version++},ct)).StatusCode);
        async Task Grant(string[] actions)=>Assert.Equal(HttpStatusCode.OK,(await admin.PutAsJsonAsync($"/api/system/access/profiles/{code}/grants",new{grants=new[]{new{viewCode="treasury",actions}},expectedVersion=version++},ct)).StatusCode);
        await Grant(["view"]);
        var assigned=await admin.PostAsJsonAsync("/api/system/access/assignments",new{subject,profileCode=code,organizationId=(Guid?)null,effectiveFrom="2020-01-01",effectiveTo=(string?)null,expectedVersion=version++},ct);
        Assert.Equal(HttpStatusCode.OK,assigned.StatusCode);
        var assignment=(await assigned.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("assignments").EnumerateArray().Single(x=>x.GetProperty("profileCode").GetString()==code).GetProperty("id").GetGuid();
        var projectionResponse=await client.GetAsync("/api/tesoreria/acceso",ct);
        Assert.True(projectionResponse.Headers.CacheControl!.Private);Assert.True(projectionResponse.Headers.CacheControl.NoStore);
        var projection=await projectionResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(projection.GetProperty("managed").GetBoolean());Assert.Equal(new[]{"view"},projection.GetProperty("actions").EnumerateArray().Select(x=>x.GetString()).ToArray());
        Assert.False(projection.TryGetProperty("subject",out _));Assert.False(projection.TryGetProperty("assignments",out _));
        foreach(var path in new[]{"/derechos-ceremoniales",$"/talleres/{org.Id}/cuadros",$"/cuadros/{statement.Id}?includeMemberDetail=true"})
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/tesoreria"+path,ct)).StatusCode);
        var regularity=new{status="up_to_date",asOfDate="2026-10-06",sourceReference="QA"};
        var member=Guid.NewGuid();
        long snapshotsBefore,paymentsBefore,auditsBefore;
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();snapshotsBefore=await db.FinancialRegularitySnapshots.LongCountAsync(ct);paymentsBefore=await db.CeremonyRightPayments.LongCountAsync(ct);auditsBefore=await db.AuditEvents.LongCountAsync(ct);
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/tesoreria/talleres/{org.Id}/regularidad",regularity,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/tesoreria/talleres/{org.Id}/miembros/{member}/regularidad",regularity,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/tesoreria/cuadros/{statement.Id}/conciliar",null,ct)).StatusCode);
        var payment=new{amount=0,paymentMethod="transfer",paymentDate="2026-10-06",reference="QA",idempotencyKey="qa"};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/ceremonias/solicitudes/{ceremony.Id}/derecho/pagos",payment,ct)).StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();Assert.Equal(snapshotsBefore,await db.FinancialRegularitySnapshots.LongCountAsync(ct));Assert.Equal(paymentsBefore,await db.CeremonyRightPayments.LongCountAsync(ct));Assert.Equal(auditsBefore,await db.AuditEvents.LongCountAsync(ct));
        }
        await Grant(["view","write"]);
        client.DefaultRequestHeaders.Add("X-Treasury-Role",InstitutionalRoles.TallerTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/tesoreria/acceso",ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/tesoreria/cuadros/{statement.Id}/conciliar",null,ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Treasury-Role");
        Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync($"/api/tesoreria/talleres/{org.Id}/regularidad",regularity,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/tesoreria/cuadros/{statement.Id}/conciliar",null,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/ceremonias/solicitudes/{ceremony.Id}/derecho/pagos",payment,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.DeleteAsync($"/api/system/access/assignments/{assignment}?expectedVersion={version}",ct)).StatusCode);
        foreach(var path in new[]{"/derechos-ceremoniales",$"/talleres/{org.Id}/cuadros",$"/cuadros/{statement.Id}",$"/talleres/{org.Id}/regularidad",$"/talleres/{org.Id}/miembros/{member}/regularidad"})
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/tesoreria"+path,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/tesoreria/cuadros/{statement.Id}/conciliar",null,ct)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/tesoreria/acceso",ct)).GetProperty("actions").EnumerateArray());
        client.DefaultRequestHeaders.Add("X-Treasury-Role",InstitutionalRoles.RegimenInterior);
        var minimal=await client.GetFromJsonAsync<JsonElement>($"/api/tesoreria/talleres/{org.Id}/regularidad",ct);
        Assert.True(minimal.TryGetProperty("status",out _));Assert.False(minimal.TryGetProperty("sourceReference",out _));Assert.False(minimal.TryGetProperty("notes",out _));
        await using var check=factory.Services.CreateAsyncScope();var dbCheck=check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.True(await dbCheck.AuditEvents.AnyAsync(x=>x.Action=="treasury.statement.reconciled"&&x.EntityId==statement.Id.ToString(),ct));
    }
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "GrandTreasury-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim("sub", Request.Headers["X-Treasury-Subject"].FirstOrDefault() ?? "grand-treasury-admin-ci"),
                new Claim(InstitutionalClaims.Scope, "order"), new Claim(InstitutionalClaims.Role, Request.Headers["X-Treasury-Role"].FirstOrDefault() ?? InstitutionalRoles.GranLogiaAdmin) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
