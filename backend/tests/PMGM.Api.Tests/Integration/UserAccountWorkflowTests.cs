using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class UserAccountWorkflowTests
{
    [Theory]
    [InlineData(true,true)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(false,false)]
    public async Task Candidates_expose_only_safe_delivery_readiness_flags_and_fail_closed(bool identityReady,bool mailReady)
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity{Available=identityReady};var mail=new Mail{Available=mailReady};
        using var factory=Factory(connection,identity,mail);using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        await Seed(factory,"active");
        using var candidates=JsonDocument.Parse(await client.GetStringAsync("/api/system/user-accounts/eligible-members",TestContext.Current.CancellationToken));
        Assert.Equal(identityReady,candidates.RootElement.GetProperty("identityConfigured").GetBoolean());
        Assert.Equal(mailReady,candidates.RootElement.GetProperty("mailConfigured").GetBoolean());
        Assert.Equal(identityReady&&mailReady,candidates.RootElement.GetProperty("deliveryConfigured").GetBoolean());
        Assert.DoesNotContain("ClientSecret",candidates.RootElement.ToString(),StringComparison.OrdinalIgnoreCase);
        if(!identityReady||!mailReady)
        {
            var response=await client.PostAsJsonAsync("/api/system/user-accounts",
                new {superuser=true,platformName="Soporte CI",platformEmail=$"soporte-{Guid.NewGuid():N}@example.invalid"},
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            Assert.Equal(0,identity.Prepares);
            Assert.Null(mail.LastAccount);
        }
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task Creation_binds_registered_email_and_compensates_delivery_or_activation_failure(bool mailFails,bool enableFails)
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity{EnableFails=enableFails};var mail=new Mail{Fails=mailFails};
        using var factory=Factory(connection,identity,mail);using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        var ids=await Seed(factory,"active");
        var response=await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=ids.Organization},TestContext.Current.CancellationToken);
        Assert.Equal(mailFails||enableFails?HttpStatusCode.ServiceUnavailable:HttpStatusCode.Created,response.StatusCode);
        Assert.Equal(ids.Email,mail.LastAccount!.Email);Assert.Equal(ids.Member,mail.LastAccount.MemberId);
        Assert.Equal(24,mail.Password!.Length);
        var text=await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);Assert.DoesNotContain(mail.Password,text);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var links=await db.Database.SqlQuery<Guid>($"SELECT \"MemberId\" AS \"Value\" FROM core.member_identity_links WHERE \"Issuer\"={identity.Issuer} AND \"Subject\"={identity.Subject} AND \"RevokedAtUtc\" IS NULL").ToListAsync(TestContext.Current.CancellationToken);
        if(mailFails||enableFails){Assert.Empty(links);Assert.False(identity.Enabled);Assert.True(identity.Removed);}
        else
        {
            Assert.Equal(ids.Member,Assert.Single(links));Assert.True(identity.Enabled);
            using var json=JsonDocument.Parse(text);Assert.True(json.RootElement.GetProperty("requiresPasswordChange").GetBoolean());
            Assert.True(json.RootElement.GetProperty("initialPasswordEmailSent").GetBoolean());
            var duplicate=await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=ids.Organization},TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        }
        var audits=await db.AuditEvents.Where(a=>a.EntityId==identity.Subject).ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(audits,a=>{Assert.DoesNotContain(mail.Password,a.MetadataJson??"");Assert.DoesNotContain(ids.Email,a.MetadataJson??"");});
        Assert.Equal(!mailFails&&!enableFails,audits.Any(a=>a.Action=="system.users.created"));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("past_active")]
    [InlineData("deceased")]
    [InlineData("voluntary_withdrawal")]
    [InlineData("forced_withdrawal")]
    public async Task Inactive_member_cannot_create_account_despite_active_membership(string status)
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        var ids=await Seed(factory,status);
        var response=await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=ids.Organization},TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Equal(0,identity.Prepares);
        using var candidates=JsonDocument.Parse(await client.GetStringAsync("/api/system/user-accounts/eligible-members",TestContext.Current.CancellationToken));
        Assert.DoesNotContain(candidates.RootElement.GetProperty("items").EnumerateArray(),x=>x.GetProperty("memberId").GetGuid()==ids.Member);
    }

    [Fact]
    public async Task Requires_binding_and_rejects_email_override_for_member_accounts()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        var ids=await Seed(factory,"active");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {},TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=Guid.NewGuid()},TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=ids.Organization,platformEmail="override@example.invalid"},TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(0,identity.Prepares);
    }

    // Regla del PO (22-10 21:31): solo existe UN Máster; ni el Máster crea otro; solo Usuarios y Superusuarios.
    [Theory]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    public async Task Only_master_may_create_regular_user_even_with_system_admin_menu(string role)
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();var mail=new Mail();
        using var factory=Factory(connection,identity,mail);using var client=factory.CreateClient();
        var ids=await Seed(factory,"active");
        client.DefaultRequestHeaders.Add("X-Account-Role",role);
        using var denied=await client.PostAsJsonAsync("/api/system/user-accounts",
            new { memberId=ids.Member,organizationId=ids.Organization },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        Assert.Equal(0,identity.Prepares);
        Assert.Null(identity.Account);
        Assert.False(identity.Enabled);
        Assert.Null(mail.LastAccount);
        Assert.Null(mail.Password);
        if(role==InstitutionalRoles.GranLogiaAdmin)
        {
            using var options=JsonDocument.Parse(await client.GetStringAsync(
                "/api/system/user-accounts/eligible-members",TestContext.Current.CancellationToken));
            Assert.False(options.RootElement.GetProperty("canCreateUser").GetBoolean());
            Assert.False(options.RootElement.GetProperty("canCreateSuperuser").GetBoolean());
        }
    }

    [Theory]
    [InlineData(InstitutionalRoles.GranLogiaAdmin,false)]
    [InlineData(InstitutionalRoles.PlatformSuperAdmin,false)]
    [InlineData(InstitutionalRoles.PlatformSuperAdmin,true)]
    public async Task Nobody_not_even_the_master_can_create_another_master(string role,bool alsoSuperuser)
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Role",role);
        var email=$"admin-{Guid.NewGuid():N}@example.invalid";
        var body=new {platformAdministrator=true,superuser=alsoSuperuser,platformName="Otro Master",platformEmail=email};
        var response=await client.PostAsJsonAsync("/api/system/user-accounts",body,TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        Assert.Equal(0,identity.Prepares);Assert.Null(identity.Account);Assert.False(identity.Enabled);
        // Concurrencia: ninguna de varias altas simultáneas llega a la identidad.
        var results=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>client.PostAsJsonAsync("/api/system/user-accounts",body,TestContext.Current.CancellationToken)));
        Assert.All(results,r=>Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode));
        Assert.Equal(0,identity.Prepares);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        if(role==InstitutionalRoles.PlatformSuperAdmin)
        {
            var audits=await db.AuditEvents.Where(a=>a.Action=="system.users.master_creation_denied").ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotEmpty(audits);Assert.All(audits,a=>Assert.DoesNotContain(email,a.MetadataJson??""));
        }
    }

    [Fact]
    public async Task Only_the_master_creates_superusers_and_they_never_get_the_global_master_role()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();var ids=await Seed(factory,"active");
        var superuser=new {superuser=true,platformName="Soporte Ficticio",platformEmail=$"soporte-{Guid.NewGuid():N}@example.invalid"};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/system/user-accounts",superuser,TestContext.Current.CancellationToken)).StatusCode);Assert.Equal(0,identity.Prepares);
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        // Un superusuario no admite Hermano/Taller ni datos incompletos.
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {superuser=true,memberId=ids.Member,organizationId=ids.Organization,platformName="X",platformEmail="x@example.invalid"},TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {superuser=true},TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(0,identity.Prepares);
        var created=await client.PostAsJsonAsync("/api/system/user-accounts",superuser,TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        Assert.Null(identity.Account!.MemberId);Assert.Null(identity.Account.OrganizationId);
        Assert.True(identity.Account.Superuser);Assert.False(identity.Account.PlatformAdministrator);
        using var json=JsonDocument.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(json.RootElement.GetProperty("platformAdministrator").GetBoolean());Assert.True(json.RootElement.GetProperty("superuser").GetBoolean());
        using var candidates=JsonDocument.Parse(await client.GetStringAsync("/api/system/user-accounts/eligible-members",TestContext.Current.CancellationToken));
        Assert.False(candidates.RootElement.GetProperty("canCreatePlatformAdministrator").GetBoolean());
        Assert.True(candidates.RootElement.GetProperty("canCreateUser").GetBoolean());
        Assert.True(candidates.RootElement.GetProperty("canCreateSuperuser").GetBoolean());
    }

    // Regla del PO (26-10 19:49): los superusuarios ven lo mismo que el Máster y crean usuarios y perfiles,
    // pero no pueden crear superusuarios ni tocar al Máster ni a otro superusuario.
    [Fact]
    public async Task Superuser_creates_regular_users_but_never_superusers_and_cannot_touch_master_or_other_superusers()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        UserAccountEndpoints.ResetRateLimits();
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();var ids=await Seed(factory,"active");
        var ct=TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Add("X-Account-Kind","superuser");
        using var options=JsonDocument.Parse(await client.GetStringAsync("/api/system/user-accounts/eligible-members",ct));
        Assert.True(options.RootElement.GetProperty("canCreateUser").GetBoolean());
        Assert.False(options.RootElement.GetProperty("canCreateSuperuser").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/system/user-accounts",new {superuser=true,platformName="Otro Soporte",platformEmail=$"o-{Guid.NewGuid():N}@example.invalid"},ct)).StatusCode);
        Assert.Equal(0,identity.Prepares);
        Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=ids.Organization},ct)).StatusCode);
        var member=Guid.NewGuid().ToString();var other=Guid.NewGuid().ToString();var master=Guid.NewGuid().ToString();
        identity.Managed[member]=new(member,"Hermano Ficticio","m@example.invalid",true,"member");
        identity.Managed[other]=new(other,"Otro Soporte","o@example.invalid",true,"superuser");
        identity.Managed[master]=new(master,"Máster","x@example.invalid",true,"platform");
        string P(string s)=>$"/api/system/user-accounts/{s}/password-recovery";
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(P(member),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(P(other),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(P(master),new{modality="email"},ct)).StatusCode);
        // El Máster sí puede restablecer a un Superusuario.
        using var asMaster=factory.CreateClient();asMaster.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        Assert.Equal(HttpStatusCode.OK,(await asMaster.PostAsJsonAsync(P(other),new{modality="email"},ct)).StatusCode);
        // Un miembro con atributo «superuser» pero sin alcance de la Orden no es operador (el claim solo vale con pmgm_scope=order).
        var access=new InstitutionalAccessService();
        var noScope=new ClaimsPrincipal(new ClaimsIdentity([new Claim(InstitutionalClaims.AccountKind,"superuser")],"t"));
        Assert.False(access.IsPlatformOperator(noScope));
    }

    [Fact]
    public async Task Superuser_cannot_assign_dynamic_profiles_to_master_or_other_superusers()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var master=factory.CreateClient();await Seed(factory,"active");
        var ct=TestContext.Current.CancellationToken;
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        async Task<int> Version(HttpClient c)=>(await c.GetFromJsonAsync<JsonElement>("/api/system/access/catalog",ct)).GetProperty("version").GetInt32();
        var code=$"sup-{Guid.NewGuid():N}"[..20];var version=await Version(master);
        Assert.Equal(HttpStatusCode.OK,(await master.PostAsJsonAsync("/api/system/access/profiles",new{code,name="Perfil soporte QA",scope="order",menuCodes=Array.Empty<string>(),expectedVersion=version},ct)).StatusCode);
        var hermano=Guid.NewGuid().ToString();var otro=Guid.NewGuid().ToString();var maestro=Guid.NewGuid().ToString();
        identity.Managed[hermano]=new(hermano,"Hermano Ficticio","h@example.invalid",true,"member");
        identity.Managed[otro]=new(otro,"Otro Soporte","o@example.invalid",true,"superuser");
        identity.Managed[maestro]=new(maestro,"Máster","x@example.invalid",true,"platform");
        using var superuser=factory.CreateClient();superuser.DefaultRequestHeaders.Add("X-Account-Kind","superuser");
        Task<HttpResponseMessage> Assign(HttpClient c,string subject)=>c.PostAsJsonAsync("/api/system/access/assignments",new{subject,profileCode=code,organizationId=(Guid?)null,effectiveFrom="2020-01-01",effectiveTo=(string?)null,expectedVersion=Version(c).GetAwaiter().GetResult()},ct);
        Assert.Equal(HttpStatusCode.Forbidden,(await Assign(superuser,otro)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Assign(superuser,maestro)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Assign(superuser,hermano)).StatusCode);
        // El Máster sí puede asignar a un Superusuario.
        Assert.Equal(HttpStatusCode.OK,(await Assign(master,otro)).StatusCode);
        // Sin poder comprobar el tipo de cuenta de destino, el Superusuario no asigna.
        identity.Available=false;
        Assert.Equal(HttpStatusCode.Forbidden,(await Assign(superuser,Guid.NewGuid().ToString())).StatusCode);
    }

    [Fact]
    public async Task Access_reset_is_master_only_audited_and_never_exposes_the_password_outside_the_response()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        UserAccountEndpoints.ResetRateLimits();
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();
        var target=Guid.NewGuid().ToString();var noEmail=Guid.NewGuid().ToString();var master=Guid.NewGuid().ToString();var pending=Guid.NewGuid().ToString();
        identity.Managed[target]=new(target,"Hermano Ficticio","reset@example.invalid",true,"member");
        identity.Managed[noEmail]=new(noEmail,"Sin Correo","",true,"member");
        identity.Managed[master]=new(master,"Máster","master@example.invalid",true,"platform");
        identity.Managed[pending]=new(pending,"Pendiente","p@example.invalid",false,"member");
        var ct=TestContext.Current.CancellationToken;
        // Sin rol de Máster: prohibido, sin tocar la identidad.
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.GranLogiaAdmin);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/system/user-accounts/{target}/password-recovery",new{modality="email"},ct)).StatusCode);
        Assert.Equal(0,identity.Emails);
        client.DefaultRequestHeaders.Remove("X-Account-Role");client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        string P(string s)=>$"/api/system/user-accounts/{s}/password-recovery";
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(P(target),new{modality="otra"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(P(target),new{modality="temporary",reason="corto"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(P("no-es-guid"),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(P(Guid.NewGuid().ToString()),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(P("ci-account-admin"),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(P(master),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(P(pending),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(P(noEmail),new{modality="email"},ct)).StatusCode);
        Assert.Equal(0,identity.Emails+identity.Temporary);
        // A) enlace por correo
        var a=await client.PostAsJsonAsync(P(target),new{modality="email"},ct);Assert.Equal(HttpStatusCode.OK,a.StatusCode);Assert.Equal(1,identity.Emails);
        Assert.Contains("no-store",a.Headers.CacheControl?.ToString()??"");
        Assert.DoesNotContain("temporaryPassword",await a.Content.ReadAsStringAsync(ct));
        // B) clave temporal mostrada una vez
        var b=await client.PostAsJsonAsync(P(noEmail),new{modality="temporary",reason="Hermano sin correo, identidad verificada en persona"},ct);
        Assert.Equal(HttpStatusCode.OK,b.StatusCode);var text=await b.Content.ReadAsStringAsync(ct);
        using var json=JsonDocument.Parse(text);Assert.Equal(identity.TemporaryPassword,json.RootElement.GetProperty("temporaryPassword").GetString());
        Assert.True(json.RootElement.GetProperty("requiresPasswordChange").GetBoolean());Assert.Equal(24,identity.TemporaryPassword!.Length);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var audits=await db.AuditEvents.Where(x=>x.Action.StartsWith("system.users.access_reset")).ToListAsync(ct);
        Assert.Contains(audits,x=>x.Action=="system.users.access_reset"&&x.EntityId==target);
        Assert.Contains(audits,x=>x.Action=="system.users.access_reset_denied");
        Assert.All(audits,x=>{Assert.DoesNotContain(identity.TemporaryPassword,x.MetadataJson??"");Assert.DoesNotContain("example.invalid",x.MetadataJson??"");});
        // Falla del proveedor: 503 genérico y auditoría de rechazo
        UserAccountEndpoints.ResetRateLimits();identity.RecoveryFails=true;
        var fail=await client.PostAsJsonAsync(P(target),new{modality="email"},ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,fail.StatusCode);
        // Límite por cuenta
        UserAccountEndpoints.ResetRateLimits();identity.RecoveryFails=false;
        for(var i=0;i<3;i++)Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(P(target),new{modality="email"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.PostAsJsonAsync(P(target),new{modality="email"},ct)).StatusCode);
    }

    [Fact]
    public async Task Inventory_includes_protected_legacy_accounts_only_for_master()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        var legacy=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(legacy,"qa.admin","QA synthetic",true,
            "legacy_protected",false,true,"provisioned"));
        var managed=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(managed,"test@example.invalid","Test synthetic",true,
            "member",true,false,"managed"));
        using var factory=Factory(connection,identity,new Mail());
        using var visitor=factory.CreateClient();
        var path="/api/system/user-accounts/inventory";
        var ct=TestContext.Current.CancellationToken;
        Assert.Equal(HttpStatusCode.Forbidden,(await visitor.GetAsync(path,ct)).StatusCode);
        visitor.DefaultRequestHeaders.Add("X-Account-Kind","superuser");
        Assert.Equal(HttpStatusCode.Forbidden,(await visitor.GetAsync(path,ct)).StatusCode);
        using var legacyWithPrivilegedRole=factory.CreateClient();
        legacyWithPrivilegedRole.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        legacyWithPrivilegedRole.DefaultRequestHeaders.Add("X-Test-Username","qa.admin");
        Assert.Equal(HttpStatusCode.Forbidden,(await legacyWithPrivilegedRole.GetAsync(path,ct)).StatusCode);
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        var response=await master.GetAsync(path,ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("no-store",response.Headers.CacheControl?.ToString()??"");
        using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var items=body.RootElement.GetProperty("items");
        Assert.Equal(2,items.GetArrayLength());
        var item=items.EnumerateArray().Single(x=>x.GetProperty("username").GetString()=="qa.admin");
        Assert.Equal(legacy,item.GetProperty("subject").GetString());
        Assert.True(item.GetProperty("protected").GetBoolean());
        Assert.False(item.GetProperty("managed").GetBoolean());
        Assert.Equal("provisioned",item.GetProperty("origin").GetString());
        Assert.DoesNotContain("password",await response.Content.ReadAsStringAsync(ct),StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lifecycle_contract_is_readonly_and_only_visible_to_unique_master()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        using var factory=Factory(connection,new Identity(),new Mail());
        const string path="/api/system/user-accounts/lifecycle-policy";
        var ct=TestContext.Current.CancellationToken;
        using var visitor=factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden,(await visitor.GetAsync(path,ct)).StatusCode);
        using var legacy=factory.CreateClient();
        legacy.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        legacy.DefaultRequestHeaders.Add("X-Test-Username","qa.admin");
        Assert.Equal(HttpStatusCode.Forbidden,(await legacy.GetAsync(path,ct)).StatusCode);
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        using var response=await master.GetAsync(path,ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("no-store",response.Headers.CacheControl?.ToString()??"");
        using var payload=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.False(payload.RootElement.GetProperty("operationsAvailable").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("reasonRequired").GetBoolean());
        Assert.Equal(10,payload.RootElement.GetProperty("reasonMinLength").GetInt32());
        Assert.Equal(200,payload.RootElement.GetProperty("reasonMaxLength").GetInt32());
        Assert.True(payload.RootElement.GetProperty("requiresLiveIdentityVerification").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("requiresEffectiveSessionRevocation").GetBoolean());
        Assert.Equal(3,payload.RootElement.GetProperty("actionNames").GetArrayLength());
    }

    [Fact]
    public async Task Lifecycle_verification_reads_effective_identity_only_for_master_and_never_changes_account()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        var subject=Guid.NewGuid().ToString();
        identity.SecuritySnapshots[subject]=new(subject,"cuenta-historica", "legacy_protected",
            true,false,true,false);
        using var factory=Factory(connection,identity,new Mail());
        var path=$"/api/system/user-accounts/{subject}/lifecycle-verification";
        var ct=TestContext.Current.CancellationToken;
        using var visitor=factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden,(await visitor.GetAsync(path,ct)).StatusCode);
        using var legacy=factory.CreateClient();
        legacy.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        legacy.DefaultRequestHeaders.Add("X-Test-Username","cuenta-historica");
        Assert.Equal(HttpStatusCode.Forbidden,(await legacy.GetAsync(path,ct)).StatusCode);
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        Assert.Equal(HttpStatusCode.NotFound,(await master.GetAsync(
            "/api/system/user-accounts/no-es-uuid/lifecycle-verification",ct)).StatusCode);
        using var response=await master.GetAsync(path,ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("no-store",response.Headers.CacheControl?.ToString()??"");
        using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(subject,body.RootElement.GetProperty("subject").GetString());
        Assert.True(body.RootElement.GetProperty("privilegedRoleDetected").GetBoolean());
        Assert.False(body.RootElement.GetProperty("effectiveRoleEvidenceComplete").GetBoolean());
        Assert.True(body.RootElement.GetProperty("requiresFurtherVerification").GetBoolean());
        Assert.False(body.RootElement.GetProperty("operationsAvailable").GetBoolean());
        Assert.False(identity.Removed);
    }

    [Fact]
    public async Task Lifecycle_operations_are_disabled_by_default_and_never_mutate_test_identity()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        using var factory=Factory(connection,identity,new Mail());
        var subject=Guid.NewGuid().ToString();
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        var ct=TestContext.Current.CancellationToken;
        foreach(var action in new[]{"activate","deactivate","delete-permanently"})
        {
            using var response=await master.PostAsJsonAsync(
                $"/api/system/user-accounts/{subject}/{action}",
                new {reason="Gestión de cuenta sintética para pruebas.",confirmation="qa.admin"},ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        }
        Assert.False(identity.Removed);
        Assert.False(identity.Enabled);
        Assert.Equal(0,identity.Prepares);
    }

    [Fact]
    public async Task Lifecycle_operations_reject_non_master_before_feature_gate()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        using var factory=Factory(connection,identity,new Mail());
        using var user=factory.CreateClient();
        user.DefaultRequestHeaders.Add("X-Account-Kind","superuser");
        using var historical=factory.CreateClient();
        historical.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        historical.DefaultRequestHeaders.Add("X-Test-Username","qa.admin");
        var subject=Guid.NewGuid().ToString();
        var ct=TestContext.Current.CancellationToken;
        foreach(var client in new[]{user,historical})
        foreach(var action in new[]{"activate","deactivate","delete-permanently"})
        {
            using var response=await client.PostAsJsonAsync(
                $"/api/system/user-accounts/{subject}/{action}",
                new {reason="Gestión de cuenta sintética para pruebas.",confirmation="qa.admin"},ct);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }
        Assert.False(identity.Removed);
    }

    [Fact]
    public async Task Lifecycle_denies_when_request_subject_differs_from_verified_master_uuid()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        var target=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(target,"cuenta-ci@example.invalid","Cuenta ficticia",
            true,"member",true,false,"managed"));
        identity.SecuritySnapshots[target]=new(target,"cuenta-ci@example.invalid","member",
            true,true,false,true);
        using var factory=FactoryLifecycleEnabled(connection,identity);
        await Seed(factory,"active");
        using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        client.DefaultRequestHeaders.Add("X-Test-Subject",Guid.NewGuid().ToString());
        var result=await client.PostAsJsonAsync(
            $"/api/system/user-accounts/{target}/deactivate",
            new {reason="Prueba con identidad de solicitante no concordante."},
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden,result.StatusCode);
        Assert.Equal(0,identity.LifecycleDeactivations);
        Assert.True(identity.SecuritySnapshots[target].Enabled);
    }

    [Fact]
    public async Task Master_with_revoked_live_role_cannot_mutate_even_when_jwt_claim_still_says_admin()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity {MasterVerified=false};
        var subject=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(subject,"bloqueado@example.invalid","Miembro ficticio",
            true,"member",true,false,"managed"));
        identity.SecuritySnapshots[subject]=new(subject,"bloqueado@example.invalid","member",
            true,true,false,true);
        using var factory=FactoryLifecycleEnabled(connection,identity);
        await Seed(factory,"active");
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        using var result=await master.PostAsJsonAsync(
            $"/api/system/user-accounts/{subject}/deactivate",
            new {reason="Intento sintético con Máster desautorizado en Keycloak."},
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden,result.StatusCode);
        Assert.Equal(0,identity.LifecycleDeactivations);
        Assert.Equal(0,identity.LifecycleDeletions);
        Assert.True(identity.SecuritySnapshots[subject].Enabled);
    }

    [Fact]
    public async Task Failed_keycloak_operation_preserves_durable_request_audit_for_reconciliation()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity{LifecycleDisableFails=true};
        var subject=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(subject,"fallo-sintetico@example.invalid","Cuenta sintética",
            true,"member",true,false,"managed"));
        identity.SecuritySnapshots[subject]=new(subject,"fallo-sintetico@example.invalid",
            "member",true,true,false,true);
        using var factory=FactoryLifecycleEnabled(connection,identity);
        await Seed(factory,"active");
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        master.DefaultRequestHeaders.Add("X-Correlation-ID","reused-ci-header");
        var ct=TestContext.Current.CancellationToken;
        using var response=await master.PostAsJsonAsync(
            $"/api/system/user-accounts/{subject}/deactivate",
            new {reason="Prueba sintética de error tras marcar cuenta deshabilitada."},ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var audits=await db.AuditEvents.Where(a=>a.EntityId==subject &&
            a.Action.StartsWith("system.users.lifecycle_")).ToListAsync(ct);
        Assert.Contains(audits,a=>a.Action=="system.users.lifecycle_requested");
        Assert.DoesNotContain(audits,a=>a.Action=="system.users.lifecycle_completed");
        // Un paso externo pudo aplicarse pese al error; se requiere conciliación,
        // nunca anunciar la operación como completada.
        Assert.Equal(0,identity.LifecycleDeletions);
        using var report=await master.GetAsync(
            $"/api/system/user-accounts/{subject}/lifecycle-reconciliation",ct);
        Assert.Equal(HttpStatusCode.OK,report.StatusCode);
        using var reportJson=JsonDocument.Parse(await report.Content.ReadAsStringAsync(ct));
        Assert.False(reportJson.RootElement.GetProperty("automaticRetryAllowed").GetBoolean());
        Assert.Contains(reportJson.RootElement.GetProperty("items").EnumerateArray(),
            item=>item.GetProperty("state").GetString()=="failed_needs_review" &&
                item.GetProperty("requiresReview").GetBoolean());
        Assert.DoesNotContain("Prueba sintética",reportJson.RootElement.ToString());
        Assert.Equal(0,reportJson.RootElement.GetProperty("unlinkedLegacyEvents").GetInt32());
        var failed=reportJson.RootElement.GetProperty("items").EnumerateArray()
            .Single(item=>item.GetProperty("state").GetString()=="failed_needs_review");
        Assert.True(Guid.TryParseExact(failed.GetProperty("operationId").GetString(),
            "N",out _));
        // Aunque el fixture vuelva a un estado aparentemente válido, no se
        // permite repetir la operación mientras el intento anterior requiere
        // conciliación manual. Reutilizar X-Correlation-ID tampoco evita el veto.
        identity.LifecycleDisableFails=false;
        identity.SecuritySnapshots[subject]=identity.SecuritySnapshots[subject] with {Enabled=true};
        using var blocked=await master.PostAsJsonAsync(
            $"/api/system/user-accounts/{subject}/deactivate",
            new {reason="Repetición sintética denegada por operación anterior."},ct);
        Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);
        Assert.Contains("prior_lifecycle_requires_review",await blocked.Content.ReadAsStringAsync(ct));
        Assert.Equal(1,identity.LifecycleDeactivations);
        using var finalReport=await master.GetAsync(
            $"/api/system/user-accounts/{subject}/lifecycle-reconciliation",ct);
        using var finalJson=JsonDocument.Parse(await finalReport.Content.ReadAsStringAsync(ct));
        var items=finalJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items,item=>item.GetProperty("state").GetString()=="failed_needs_review" &&
            item.GetProperty("requiresReview").GetBoolean());
        Assert.Contains(items,item=>item.GetProperty("state").GetString()=="denied_no_external_mutation" &&
            !item.GetProperty("requiresReview").GetBoolean());
        var operationIds=items.Select(item=>item.GetProperty("operationId").GetString())
            .Distinct().ToArray();
        Assert.Collection(operationIds,
            id=>Assert.False(string.IsNullOrWhiteSpace(id)),
            id=>Assert.False(string.IsNullOrWhiteSpace(id)));
        using var visitor=factory.CreateClient();
        visitor.DefaultRequestHeaders.Add("X-Account-Kind","superuser");
        Assert.Equal(HttpStatusCode.Forbidden,(await visitor.GetAsync(
            $"/api/system/user-accounts/{subject}/lifecycle-reconciliation",ct)).StatusCode);
    }

    [Fact]
    public async Task Durable_pending_request_from_prior_process_blocks_new_mutation()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        var subject=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(subject,"pendiente-ci@example.invalid","Cuenta ficticia",
            true,"member",true,false,"managed"));
        identity.SecuritySnapshots[subject]=new(subject,"pendiente-ci@example.invalid",
            "member",true,true,false,true);
        using var factory=FactoryLifecycleEnabled(connection,identity);
        await Seed(factory,"active");
        var ct=TestContext.Current.CancellationToken;
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            db.AuditEvents.Add(new PMGM.Api.Modules.Audit.Entities.AuditEvent {
                Action="system.users.lifecycle_requested",
                EntityType="UserAccount", EntityId=subject,
                Result=AuditResults.Observed,
                CorrelationId="fixture-de-proceso-interrumpido",
                MetadataJson=JsonSerializer.Serialize(new {
                    operationId=Guid.NewGuid().ToString("N"),action="deactivate"
                })
            });
            await db.SaveChangesAsync(ct);
        }
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        using var response=await master.PostAsJsonAsync(
            $"/api/system/user-accounts/{subject}/deactivate",
            new {reason="Intento sintético después de proceso interrumpido."},ct);
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        Assert.Contains("prior_lifecycle_requires_review",
            await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(0,identity.LifecycleDeactivations);
        using var report=await master.GetAsync(
            $"/api/system/user-accounts/{subject}/lifecycle-reconciliation",ct);
        Assert.Equal(HttpStatusCode.OK,report.StatusCode);
        using var json=JsonDocument.Parse(await report.Content.ReadAsStringAsync(ct));
        Assert.Contains(json.RootElement.GetProperty("items").EnumerateArray(),
            item=>item.GetProperty("state").GetString()=="unresolved_needs_review" &&
                item.GetProperty("requiresReview").GetBoolean());
        Assert.Contains(json.RootElement.GetProperty("items").EnumerateArray(),
            item=>item.GetProperty("state").GetString()=="denied_no_external_mutation");
    }

    [Fact]
    public async Task Already_completed_operation_does_not_false_block_new_legal_mutation()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        var subject=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(subject,"completa-ci@example.invalid","Cuenta ficticia",
            true,"member",true,false,"managed"));
        identity.SecuritySnapshots[subject]=new(subject,"completa-ci@example.invalid",
            "member",true,true,false,true);
        using var factory=FactoryLifecycleEnabled(connection,identity);
        await Seed(factory,"active");
        var ct=TestContext.Current.CancellationToken;
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var operationId=Guid.NewGuid().ToString("N");
            foreach(var (action,result) in new[] {
                ("system.users.lifecycle_requested",AuditResults.Observed),
                ("system.users.lifecycle_completed",AuditResults.Success)
            })
                db.AuditEvents.Add(new PMGM.Api.Modules.Audit.Entities.AuditEvent {
                    Action=action,EntityType="UserAccount",EntityId=subject,
                    Result=result,CorrelationId="synthetic-complete",
                    MetadataJson=JsonSerializer.Serialize(new {operationId,action="activate"})
                });
            await db.SaveChangesAsync(ct);
        }
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        using var response=await master.PostAsJsonAsync(
            $"/api/system/user-accounts/{subject}/deactivate",
            new {reason="Desactivación ficticia posterior a operación ya cerrada."},ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal(1,identity.LifecycleDeactivations);
    }

    [Fact]
    public async Task Enabled_lifecycle_flag_runs_deactivation_with_immutable_audit_and_synthetic_identity()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        var subject=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(subject,"sintetico@example.invalid","Miembro sintético",
            true,"member",true,false,"managed"));
        identity.SecuritySnapshots[subject]=new(subject,"sintetico@example.invalid","member",
            true,true,false,true);
        using var factory=FactoryLifecycleEnabled(connection,identity);
        await Seed(factory,"active");
        using var client=factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        using var response=await client.PostAsJsonAsync(
            $"/api/system/user-accounts/{subject}/deactivate",
            new {reason="Desactivar cuenta de prueba exclusivamente sintética."},
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Equal(1,identity.LifecycleDeactivations);
        Assert.Equal(0,identity.LifecycleDeletions);
        Assert.False(identity.SecuritySnapshots[subject].Enabled);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var events=await db.AuditEvents.Where(e=>e.EntityId==subject &&
            e.Action.StartsWith("system.users.lifecycle_")).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(events,e=>e.Action=="system.users.lifecycle_requested");
        Assert.Contains(events,e=>e.Action=="system.users.lifecycle_completed");
        Assert.DoesNotContain(events,e=>e.Action=="system.users.lifecycle_failed");
        using var report=await client.GetAsync(
            $"/api/system/user-accounts/{subject}/lifecycle-reconciliation",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK,report.StatusCode);
        using var reportJson=JsonDocument.Parse(
            await report.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(reportJson.RootElement.GetProperty("items").EnumerateArray(),
            item=>item.GetProperty("state").GetString()=="completed" &&
                !item.GetProperty("requiresReview").GetBoolean());
    }

    [Fact]
    public async Task Verified_master_may_delete_synthetic_legacy_qa_admin_but_not_admin()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if(string.IsNullOrWhiteSpace(connection)) return;
        var identity=new Identity();
        var qaSubject=Guid.NewGuid().ToString();
        var adminSubject=Guid.NewGuid().ToString();
        identity.InventoryItems.Add(new(qaSubject,"qa.admin","QA sintético",
            true,"legacy_protected",false,true,"provisioned"));
        identity.SecuritySnapshots[qaSubject]=new(qaSubject,"qa.admin","",
            true,false,true,true);
        identity.InventoryItems.Add(new(adminSubject,"admin","Máster sintético",
            true,"legacy_protected",false,true,"provisioned"));
        identity.SecuritySnapshots[adminSubject]=new(adminSubject,"admin","",
            true,false,true,true);
        using var factory=FactoryLifecycleEnabled(connection,identity);
        await Seed(factory,"active");
        using var master=factory.CreateClient();
        master.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        var ct=TestContext.Current.CancellationToken;
        var body=new {reason="Eliminación ficticia autorizada para prueba.",
            confirmation="qa.admin"};
        using var denied=await master.PostAsJsonAsync(
            $"/api/system/user-accounts/{adminSubject}/delete-permanently",body,ct);
        Assert.Equal(HttpStatusCode.Conflict,denied.StatusCode);
        Assert.Equal(0,identity.LifecycleDeletions);
        // Rechazo de política antes de tocar Keycloak: cerrado, no pendiente.
        using var deniedReport=await master.GetAsync(
            $"/api/system/user-accounts/{adminSubject}/lifecycle-reconciliation",ct);
        Assert.Equal(HttpStatusCode.OK,deniedReport.StatusCode);
        using var deniedJson=JsonDocument.Parse(await deniedReport.Content.ReadAsStringAsync(ct));
        Assert.Contains(deniedJson.RootElement.GetProperty("items").EnumerateArray(),
            item=>item.GetProperty("state").GetString()=="denied_no_external_mutation" &&
                !item.GetProperty("requiresReview").GetBoolean());
        await using(var check=factory.Services.CreateAsyncScope())
        {
            var checkDb=check.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.True(await checkDb.AuditEvents.AnyAsync(e=>e.EntityId==adminSubject &&
                e.Action=="system.users.lifecycle_preflight_rejected",ct));
        }
        using var response=await master.PostAsJsonAsync(
            $"/api/system/user-accounts/{qaSubject}/delete-permanently",body,ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.True(identity.Removed);
        Assert.Equal(1,identity.LifecycleDeletions);
        Assert.Equal(1,identity.LifecycleDeactivations);
        Assert.Contains(adminSubject,identity.SecuritySnapshots.Keys);
        Assert.DoesNotContain(qaSubject,identity.SecuritySnapshots.Keys);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.True(await db.Memberships.AnyAsync(ct));
        Assert.True(await db.AuditEvents.AnyAsync(e=>e.EntityId==qaSubject &&
            e.Action=="system.users.lifecycle_completed",ct));
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> FactoryLifecycleEnabled(
        string connection,Identity identity)
        => Factory(connection,identity,new Mail()).WithWebHostBuilder(builder=>
            builder.ConfigureAppConfiguration((_,configuration)=>
                configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                    ["UserAccounts:Identity:RequireLiveTokenIntrospection"]="true",
                    ["UserAccounts:Identity:EnableLifecycleMutations"]="true"
                })));

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string connection,Identity identity,Mail mail)
        => new PmgmWebApplicationFactory(connection).WithWebHostBuilder(builder=>builder.ConfigureTestServices(services=>
        {
            services.AddSingleton<IAccountIdentityProvider>(identity);services.AddSingleton<IInitialAccountMail>(mail);
            services.AddAuthentication(o=>{o.DefaultAuthenticateScheme=AccountAuth.SchemeName;o.DefaultChallengeScheme=AccountAuth.SchemeName;o.DefaultForbidScheme=AccountAuth.SchemeName;})
                .AddScheme<AuthenticationSchemeOptions,AccountAuth>(AccountAuth.SchemeName,_=>{});
        }));
    private static async Task<(Guid Member,Guid Organization,string Email)> Seed(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,string status)
    {
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var org=new Organization{Name=$"Taller cuentas CI {Guid.NewGuid():N}",Type="workshop",Number="CI",City="Santiago",Country="Chile",OrienteCode="santiago"};
        var person=new Person{FirstNames="Hermano",LastNames="Ficticio",Email=$"cuenta-{Guid.NewGuid():N}@example.invalid"};
        var member=new Member{Person=person,PersonId=person.Id};
        db.AddRange(org,person,member,new Membership{Member=member,MemberId=member.Id,Organization=org,OrganizationId=org.Id,MembershipType="regular",Status="active",StartDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)},
            new InstitutionalStatusEvent{Member=member,MemberId=member.Id,EventType=status,EffectiveDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)});
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);return(member.Id,org.Id,person.Email!);
    }
    private sealed class Identity:IAccountIdentityProvider,IAccountIdentityInventory,IAccountIdentitySecurityReader,IAccountIdentityLifecycleCommands,IUniqueMasterIdentityVerifier
    {
        public bool Available=true;public bool Configured=>Available;public string Issuer{get;}=$"https://identity.example.invalid/{Guid.NewGuid():N}";
        public string Subject{get;}=Guid.NewGuid().ToString();public bool Enabled,Removed,EnableFails;public int Prepares;public AccountIdentity? Account;
        public Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account,string password,CancellationToken ct)
        {Prepares++;if(Enabled)throw new AccountConflictException();Account=account;return Task.FromResult(new PreparedIdentity(Subject,true));}
        public Task EnableAsync(string subject,CancellationToken ct){if(EnableFails)throw new AccountDeliveryException();Enabled=true;return Task.CompletedTask;}
        public Task RemoveAsync(string subject,CancellationToken ct){Enabled=false;Removed=true;return Task.CompletedTask;}
        public Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<AccountUser>>([]);
        public List<AccountInventoryItem> InventoryItems { get; }=[];
        public Task<IReadOnlyList<AccountInventoryItem>> ListInventoryAsync(CancellationToken ct)
            =>Task.FromResult<IReadOnlyList<AccountInventoryItem>>(InventoryItems);
        public Dictionary<string,AccountIdentitySecuritySnapshot> SecuritySnapshots { get; }=new();
        public Task<AccountIdentitySecuritySnapshot?> ReadSecuritySnapshotAsync(string subject,CancellationToken ct)
            =>Task.FromResult(SecuritySnapshots.GetValueOrDefault(subject));
        public bool MasterVerified=true;
        public int LifecycleActivations,LifecycleDeactivations,LifecycleDeletions;
        public bool LifecycleDisableFails;
        public Task<bool> VerifyUniqueMasterDistinctFromAsync(string actorSubject,string subject,CancellationToken ct)
            =>Task.FromResult(MasterVerified &&
                string.Equals(actorSubject,"ci-account-admin",StringComparison.Ordinal) &&
                !string.Equals(actorSubject,subject,StringComparison.OrdinalIgnoreCase));
        public Task DisableAndLogoutAsync(string subject,CancellationToken ct)
        {
            LifecycleDeactivations++;
            if(SecuritySnapshots.TryGetValue(subject,out var snapshot))
                SecuritySnapshots[subject]=snapshot with {Enabled=false};
            Enabled=false;
            if(LifecycleDisableFails) throw new AccountDeliveryException();
            return Task.CompletedTask;
        }
        public Task ReactivateAsync(string subject,CancellationToken ct)
        {
            LifecycleActivations++;
            if(SecuritySnapshots.TryGetValue(subject,out var snapshot))
                SecuritySnapshots[subject]=snapshot with {Enabled=true};
            Enabled=true;return Task.CompletedTask;
        }
        public Task DeleteDisabledAsync(string subject,CancellationToken ct)
        {
            if(!SecuritySnapshots.TryGetValue(subject,out var snapshot) || snapshot.Enabled)
                throw new AccountConflictException();
            LifecycleDeletions++;SecuritySnapshots.Remove(subject);Removed=true;
            return Task.CompletedTask;
        }
        public Dictionary<string,AccountUser> Managed{get;}=new();public int Emails,Temporary;public string? TemporaryPassword;public bool RecoveryFails;
        public Task<AccountUser?> FindManagedAsync(string subject,CancellationToken ct)=>Task.FromResult(Managed.GetValueOrDefault(subject));
        public Task<AccountClaims?> GetClaimsAsync(string subject,CancellationToken ct)=>Task.FromResult<AccountClaims?>(null);
        public Task SendPasswordRecoveryEmailAsync(string subject,CancellationToken ct){if(RecoveryFails)throw new AccountDeliveryException();Emails++;return Task.CompletedTask;}
        public Task SetTemporaryPasswordAsync(string subject,string password,CancellationToken ct){if(RecoveryFails)throw new AccountDeliveryException();Temporary++;TemporaryPassword=password;return Task.CompletedTask;}
    }
    private sealed class Mail:IInitialAccountMail
    {
        public bool Available=true;public bool Configured=>Available;public bool Fails;public AccountIdentity? LastAccount;public string? Password;
        public Task SendAsync(AccountIdentity account,string password,CancellationToken ct){LastAccount=account;Password=password;if(Fails)throw new AccountDeliveryException();return Task.CompletedTask;}
    }
}
internal sealed class AccountAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    public const string SchemeName="PMGM-Account-Test";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var kind=Request.Headers["X-Account-Kind"].FirstOrDefault();
        var role=Request.Headers["X-Account-Role"].FirstOrDefault()??(kind is null?InstitutionalRoles.GranLogiaAdmin:null);
        var testSubject=Request.Headers["X-Test-Subject"].FirstOrDefault()??"ci-account-admin";
        var claims=new List<Claim>{new("sub",testSubject),new(ClaimTypes.NameIdentifier,testSubject),new(InstitutionalClaims.Scope,"order")};
        if(role is not null)claims.Add(new Claim(InstitutionalClaims.Role,role));
        if(role==InstitutionalRoles.PlatformSuperAdmin)
            claims.Add(new Claim("preferred_username",Request.Headers["X-Test-Username"].FirstOrDefault()??"admin"));
        if(kind is not null)claims.Add(new Claim(InstitutionalClaims.AccountKind,kind));
        var principal=new ClaimsPrincipal(new ClaimsIdentity(claims,SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,SchemeName)));
    }
}
