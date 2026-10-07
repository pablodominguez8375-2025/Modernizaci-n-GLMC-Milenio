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
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task Creation_binds_registered_email_and_compensates_delivery_or_activation_failure(bool mailFails,bool enableFails)
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity{EnableFails=enableFails};var mail=new Mail{Fails=mailFails};
        using var factory=Factory(connection,identity,mail);using var client=factory.CreateClient();
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
    [InlineData("deceased")]
    [InlineData("voluntary_withdrawal")]
    [InlineData("forced_withdrawal")]
    public async Task Inactive_member_cannot_create_account_despite_active_membership(string status)
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();var ids=await Seed(factory,status);
        var response=await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=ids.Organization},TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Equal(0,identity.Prepares);
        using var candidates=JsonDocument.Parse(await client.GetStringAsync("/api/system/user-accounts/eligible-members",TestContext.Current.CancellationToken));
        Assert.DoesNotContain(candidates.RootElement.GetProperty("items").EnumerateArray(),x=>x.GetProperty("memberId").GetGuid()==ids.Member);
    }

    [Fact]
    public async Task Requires_binding_rejects_email_override_and_limits_platform_exception_to_superadmin()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");if(string.IsNullOrWhiteSpace(connection))return;
        var identity=new Identity();using var factory=Factory(connection,identity,new Mail());using var client=factory.CreateClient();var ids=await Seed(factory,"active");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {},TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=Guid.NewGuid()},TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/system/user-accounts",new {memberId=ids.Member,organizationId=ids.Organization,platformEmail="override@example.invalid"},TestContext.Current.CancellationToken)).StatusCode);
        var platform=new {platformAdministrator=true,platformName="Admin Ficticio",platformEmail=$"admin-{Guid.NewGuid():N}@example.invalid"};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/system/user-accounts",platform,TestContext.Current.CancellationToken)).StatusCode);Assert.Equal(0,identity.Prepares);
        client.DefaultRequestHeaders.Add("X-Account-Role",InstitutionalRoles.PlatformSuperAdmin);
        Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync("/api/system/user-accounts",platform,TestContext.Current.CancellationToken)).StatusCode);
        Assert.Null(identity.Account!.MemberId);Assert.Null(identity.Account.OrganizationId);Assert.True(identity.Account.PlatformAdministrator);
    }

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
    private sealed class Identity:IAccountIdentityProvider
    {
        public bool Configured=>true;public string Issuer{get;}=$"https://identity.example.invalid/{Guid.NewGuid():N}";
        public string Subject{get;}=Guid.NewGuid().ToString();public bool Enabled,Removed,EnableFails;public int Prepares;public AccountIdentity? Account;
        public Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account,string password,CancellationToken ct)
        {Prepares++;if(Enabled)throw new AccountConflictException();Account=account;return Task.FromResult(new PreparedIdentity(Subject,true));}
        public Task EnableAsync(string subject,CancellationToken ct){if(EnableFails)throw new AccountDeliveryException();Enabled=true;return Task.CompletedTask;}
        public Task RemoveAsync(string subject,CancellationToken ct){Enabled=false;Removed=true;return Task.CompletedTask;}
        public Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<AccountUser>>([]);
    }
    private sealed class Mail:IInitialAccountMail
    {
        public bool Configured=>true;public bool Fails;public AccountIdentity? LastAccount;public string? Password;
        public Task SendAsync(AccountIdentity account,string password,CancellationToken ct){LastAccount=account;Password=password;if(Fails)throw new AccountDeliveryException();return Task.CompletedTask;}
    }
}
internal sealed class AccountAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    public const string SchemeName="PMGM-Account-Test";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role=Request.Headers["X-Account-Role"].FirstOrDefault()??InstitutionalRoles.GranLogiaAdmin;
        var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","ci-account-admin"),new Claim(ClaimTypes.NameIdentifier,"ci-account-admin"),new Claim(InstitutionalClaims.Scope,"order"),new Claim(InstitutionalClaims.Role,role)],SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,SchemeName)));
    }
}
