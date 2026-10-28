using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PMGM.Api.Modules.UserAccounts;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>La lectura de seguridad no modifica cuentas ni depende del atributo visual.</summary>
public sealed class AccountIdentitySecuritySnapshotTests
{
    private static KeycloakAccountIdentityProvider Provider(HttpMessageHandler handler)
        => new(new HttpClient(handler),Options.Create(new AccountIdentityOptions {
            ServerUrl="https://identity.example.invalid",
            Issuer="https://identity.example.invalid/realms/pmgm",
            ClientSecret="synthetic-test-only"
        }));

    [Fact]
    public async Task Exact_subject_is_read_with_no_mutations_and_managed_account_is_not_auto_privileged()
    {
        using var handler=new SecurityHandler();
        var snapshot=await Provider(handler).ReadSecuritySnapshotAsync(
            handler.Subject,TestContext.Current.CancellationToken);
        Assert.NotNull(snapshot);
        Assert.Equal(handler.Subject,snapshot.Subject);
        Assert.True(snapshot.Managed);
        Assert.True(snapshot.Enabled);
        Assert.False(snapshot.PrivilegedRoleDetected);
        Assert.True(snapshot.EffectiveRoleEvidenceComplete);
        Assert.All(handler.Methods,m=>Assert.Equal("GET",m));
        Assert.Contains(handler.Paths,p=>p.EndsWith("/role-mappings/realm/composite",StringComparison.Ordinal));
        Assert.Contains(handler.Paths,p=>p.EndsWith("/role-mappings",StringComparison.Ordinal));
        Assert.Contains(handler.Paths,p=>p.Contains("/groups?first=0&max=1",StringComparison.Ordinal));
    }

    [Fact]
    public async Task Privileged_realm_role_is_detected_from_effective_composite_mapping()
    {
        using var handler=new SecurityHandler{RealmRole="platform_superadmin"};
        var snapshot=await Provider(handler).ReadSecuritySnapshotAsync(
            handler.Subject,TestContext.Current.CancellationToken);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.PrivilegedRoleDetected);
    }

    [Theory]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task Client_or_group_roles_make_evidence_incomplete(bool clientRoles,bool groups)
    {
        using var handler=new SecurityHandler{ClientRoles=clientRoles,Groups=groups};
        var snapshot=await Provider(handler).ReadSecuritySnapshotAsync(
            handler.Subject,TestContext.Current.CancellationToken);
        Assert.NotNull(snapshot);
        Assert.False(snapshot.EffectiveRoleEvidenceComplete);
    }

    [Fact]
    public async Task Unexpected_user_id_is_rejected_without_any_write()
    {
        using var handler=new SecurityHandler{WrongId=true};
        await Assert.ThrowsAsync<AccountDeliveryException>(()=>
            Provider(handler).ReadSecuritySnapshotAsync(handler.Subject,TestContext.Current.CancellationToken));
        Assert.All(handler.Methods,m=>Assert.Equal("GET",m));
    }

    [Fact]
    public async Task Role_provider_failure_rejects_security_verification()
    {
        using var handler=new SecurityHandler{FailRealmRoleRead=true};
        await Assert.ThrowsAsync<AccountDeliveryException>(()=>
            Provider(handler).ReadSecuritySnapshotAsync(
                handler.Subject,TestContext.Current.CancellationToken));
        Assert.All(handler.Methods,m=>Assert.Equal("GET",m));
    }

    [Fact]
    public async Task Invalid_subject_does_not_even_query_identity_provider()
    {
        using var handler=new SecurityHandler();
        Assert.Null(await Provider(handler).ReadSecuritySnapshotAsync(
            "not-a-guid",TestContext.Current.CancellationToken));
        Assert.Empty(handler.Paths);
    }

    private sealed class SecurityHandler:HttpMessageHandler
    {
        public readonly string Subject=Guid.NewGuid().ToString();
        public string? RealmRole;
        public bool ClientRoles;
        public bool Groups;
        public bool WrongId;
        public bool FailRealmRoleRead;
        public readonly List<string> Methods=[];
        public readonly List<string> Paths=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/token",StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                    Content=JsonContent.Create(new { access_token="synthetic-only" })
                });
            Methods.Add(request.Method.Method);
            Paths.Add(request.RequestUri.ToString());
            if(FailRealmRoleRead && path.EndsWith("/role-mappings/realm/composite",StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            object body;
            if(path.EndsWith("/role-mappings/realm/composite",StringComparison.Ordinal))
                body=RealmRole is null ? Array.Empty<object>() : new object[]{ new {name=RealmRole} };
            else if(path.EndsWith("/role-mappings",StringComparison.Ordinal))
                body=new {realmMappings=Array.Empty<object>(),clientMappings=
                    ClientRoles ? new Dictionary<string,object> {["fake-client"]=new {roles=new[]{"manage-users"}}}
                                : new Dictionary<string,object>()};
            else if(path.EndsWith("/groups",StringComparison.Ordinal))
                body=Groups ? new object[]{new {id="synthetic-group"}} : Array.Empty<object>();
            else
                body=new {id=WrongId?Guid.NewGuid().ToString():Subject,
                    username="miembro@example.invalid",enabled=true,
                    attributes=new Dictionary<string,string[]> {
                        ["pmgm_managed"]=["active-member-accounts"],
                        ["pmgm_account_kind"]=["member"]}};
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(body)});
        }
    }
}
