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
using PMGM.Api.Modules.DocumentManagement;
using Xunit;
namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class DynamicViewAccessHttpTests
{
    private static readonly string? Connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string connection)
        => new PmgmWebApplicationFactory(connection).WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            // Resolve the normal endpoint dependencies before the filter, without unconfigured S3.
            s.AddSingleton<IDocumentObjectStore>(new InMemoryDocumentObjectStore());
            s.AddAuthentication(o =>
            { o.DefaultAuthenticateScheme=Auth.Name; o.DefaultChallengeScheme=Auth.Name; o.DefaultForbidScheme=Auth.Name; })
                .AddScheme<AuthenticationSchemeOptions,Auth>(Auth.Name,_=>{});
        }));
    private static async Task Save(PmgmDbContext db,DynamicAccessCatalog catalog,CancellationToken ct)
    {
        db.DynamicAccessSnapshots.Add(new(){Version=catalog.Version+1,Payload=JsonSerializer.Serialize(catalog with{Version=catalog.Version+1},new JsonSerializerOptions(JsonSerializerDefaults.Web))});
        await db.SaveChangesAsync(ct);
    }
    [Fact]
    public async Task Managed_subject_cannot_read_ungranted_modules_but_administrator_can_still_create_and_edit_profiles()
    {
        if(string.IsNullOrWhiteSpace(Connection))return;var ct=TestContext.Current.CancellationToken;
        using var factory=Factory(Connection);using var client=factory.CreateClient();var subject="view-"+Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("X-View-Subject",subject);var org=new Organization{Name="Taller sintético",Type="workshop"};
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();await db.Database.MigrateAsync(ct);db.Organizations.Add(org);
            var c=await DynamicAccessEndpoints.LoadAsync(db,ct);var code="qa-"+Guid.NewGuid().ToString("N");
            c.Profiles.Add(new(Guid.NewGuid(),code,"Sin acceso","order",false,true,[],[]));c.Assignments.Add(new(Guid.NewGuid(),subject,code,null,new(2020,1,1),null,true));await Save(db,c,ct);
        }
        foreach(var path in new[]{"/api/member-self/profile","/api/membership/me/hospitalaria",$"/api/members?organizationId={org.Id}",
            $"/api/institutional/organizations/{org.Id}/profile",$"/api/gestion-logial/talleres/{org.Id}/tenidas",
            "/api/regimen-interior/summary","/api/admisiones/expedientes","/api/insinuados/revision-gran-secretaria","/api/institutional/ceremonias/bandeja",
            "/api/gran-secretaria/documentos","/api/biblioteca","/api/documentos/colecciones","/api/grand-archive/",
            "/api/calendar?fromUtc=2026-10-01T00:00:00Z&toUtc=2026-11-01T00:00:00Z","/api/calendar/ics?fromUtc=2026-10-01T00:00:00Z&toUtc=2026-11-01T00:00:00Z",
            "/api/notifications/me","/api/system/settings/","/api/system/audit-events"})
        {
            var response=await client.GetAsync(path,ct);
            Assert.True(response.StatusCode==HttpStatusCode.Forbidden,$"{path}: {response.StatusCode}");
        }
        var projection=await client.GetFromJsonAsync<JsonElement>("/api/session/view-access",ct);
        Assert.Empty(projection.GetProperty("views").GetProperty("lodge").EnumerateArray());Assert.False(projection.TryGetProperty("assignments",out _));
        var catalog=await client.GetFromJsonAsync<JsonElement>("/api/system/access/catalog",ct);var version=catalog.GetProperty("version").GetInt32();var newCode="qa-"+Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/system/access/profiles",new{code=newCode,name="Creado por administrador",scope="order",menuCodes=new[]{"lodge"},expectedVersion=version},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/system/access/profiles/{newCode}",new{name="Editado por administrador",scope="order",menuCodes=new[]{"lodge"},expectedVersion=version+1},ct)).StatusCode);
    }
    [Fact]
    public async Task Independent_actions_resolve_real_record_origin_audit_print_and_revoke_authenticated_session()
    {
        if(string.IsNullOrWhiteSpace(Connection))return;var ct=TestContext.Current.CancellationToken;
        using var factory=Factory(Connection);using var client=factory.CreateClient();var subject="local-"+Guid.NewGuid().ToString("N");
        var org=new Organization{Name="Permisos logiales QA",Type="workshop"};var other=new Organization{Name="Otro Taller QA",Type="workshop"};
        var code="qa-"+Guid.NewGuid().ToString("N");var assignment=Guid.NewGuid();
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();await db.Database.MigrateAsync(ct);
            await scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>().Database.MigrateAsync(ct);db.Organizations.AddRange(org,other);
            var c=await DynamicAccessEndpoints.LoadAsync(db,ct);c.Profiles.Add(new(Guid.NewGuid(),code,"Crear sin registrar","lodge",false,true,["lodge"],[new("lodge",["view","create"])]));
            c.Assignments.Add(new(assignment,subject,code,org.Id,new(2020,1,1),null,true));
            var full=code+"-other";c.Profiles.Add(new(Guid.NewGuid(),full,"Completo otro Taller","lodge",false,true,["lodge"],[new("lodge",DynamicAccessEndpoints.AllowedActions)]));c.Assignments.Add(new(Guid.NewGuid(),subject,full,other.Id,new(2020,1,1),null,true));await Save(db,c,ct);
        }
        client.DefaultRequestHeaders.Add("X-View-Subject",subject);client.DefaultRequestHeaders.Add("X-View-Role",InstitutionalRoles.TallerSecretaria);client.DefaultRequestHeaders.Add("X-View-Organization",org.Id.ToString());
        var root=$"/api/gestion-logial/talleres/{org.Id}/tenidas";
        var created=await client.PostAsJsonAsync(root,new{meetingDate="2026-10-07",meetingType="regular",grade="apprentice",modality="in_person",locationReference="QA",title="Sintética"},ct);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var id=(await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(root,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/gestion-logial/tenidas/{id}/realizar?organizationId={other.Id}",null,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/session/views/lodge/print?organizationId={org.Id}",null,ct)).StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();var c=await DynamicAccessEndpoints.LoadAsync(db,ct);var i=c.Profiles.FindIndex(p=>p.Code==code);c.Profiles[i]=c.Profiles[i] with{Grants=[new("lodge",["view","create","print"])]};await Save(db,c,ct);
        }
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/session/views/lodge/print?organizationId={org.Id}",null,ct)).StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();var c=await DynamicAccessEndpoints.LoadAsync(db,ct);var i=c.Assignments.FindIndex(a=>a.Id==assignment);c.Assignments[i]=c.Assignments[i] with{IsActive=false};await Save(db,c,ct);
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(root,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/session/views/lodge/print?organizationId={org.Id}",null,ct)).StatusCode);
        // A complete technical grant cannot turn a local secretary into the Gran Archivero.
        var otherSubject="no-authority-"+Guid.NewGuid().ToString("N");
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();var c=await DynamicAccessEndpoints.LoadAsync(db,ct);var fullCode="qa-"+Guid.NewGuid().ToString("N");
            c.Profiles.Add(new(Guid.NewGuid(),fullCode,"Archivo técnico completo","order",false,true,["documents"],[new("grandarchive",DynamicAccessEndpoints.AllowedActions)]));
            c.Assignments.Add(new(Guid.NewGuid(),otherSubject,fullCode,null,new(2020,1,1),null,true));await Save(db,c,ct);
        }
        client.DefaultRequestHeaders.Remove("X-View-Subject");client.DefaultRequestHeaders.Add("X-View-Subject",otherSubject);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/grand-archive/",ct)).StatusCode);
        await using var check=factory.Services.CreateAsyncScope();var checkDb=check.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(1,await checkDb.AuditEvents.CountAsync(a=>a.Action=="system.access.view_print_requested"&&a.OrganizationId==org.Id,ct));
    }
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        public const string Name="DynamicView-CI";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims=new List<Claim>{new("sub",Request.Headers["X-View-Subject"].FirstOrDefault()??"view-ci"),new(InstitutionalClaims.Scope,"order"),new(InstitutionalClaims.Role,Request.Headers["X-View-Role"].FirstOrDefault()??InstitutionalRoles.GranLogiaAdmin)};
            if(Guid.TryParse(Request.Headers["X-View-Organization"].FirstOrDefault(),out var org))claims.Add(new(InstitutionalClaims.Organization,org.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims,Name)),Name)));
        }
    }
}
