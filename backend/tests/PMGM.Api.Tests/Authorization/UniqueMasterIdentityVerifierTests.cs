using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PMGM.Api.Modules.UserAccounts;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class UniqueMasterIdentityVerifierTests
{
    private static KeycloakAccountIdentityProvider Provider(HttpMessageHandler handler)
        => new(new HttpClient(handler),Options.Create(new AccountIdentityOptions {
            ServerUrl="https://identity.example.invalid",
            Issuer="https://identity.example.invalid/realms/pmgm",ClientSecret="synthetic-only"
        }));

    [Fact]
    public async Task Enabled_master_with_platform_role_and_distinct_uuid_is_verified()
    {
        using var handler=new MasterHandler();
        Assert.True(await Provider(handler).VerifyUniqueMasterDistinctFromAsync(
            handler.Master,handler.Target,TestContext.Current.CancellationToken));
        Assert.Equal(2,handler.Paths.Count);
        Assert.All(handler.Methods,m=>Assert.Equal("GET",m));
    }

    [Theory]
    [InlineData(true,false,false,false)]
    [InlineData(false,true,false,false)]
    [InlineData(false,false,true,false)]
    [InlineData(false,false,false,true)]
    public async Task Missing_or_disabled_or_same_subject_or_unprivileged_master_fails_closed(
        bool missing,bool disabled,bool sameSubject,bool noMasterRole)
    {
        using var handler=new MasterHandler{Missing=missing,Disabled=disabled,
            SameSubject=sameSubject,NoMasterRole=noMasterRole};
        Assert.False(await Provider(handler).VerifyUniqueMasterDistinctFromAsync(
            handler.Master,handler.Target,TestContext.Current.CancellationToken));
        Assert.All(handler.Methods,m=>Assert.Equal("GET",m));
    }

    [Fact]
    public async Task Wrong_username_is_denied_even_if_platform_role_exists()
    {
        using var handler=new MasterHandler {WrongUsername=true};
        Assert.False(await Provider(handler).VerifyUniqueMasterDistinctFromAsync(
            handler.Master,handler.Target,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unavailable_master_role_provider_fails_closed()
    {
        using var handler=new MasterHandler {Unavailable=true};
        await Assert.ThrowsAsync<AccountDeliveryException>(()=>Provider(handler)
            .VerifyUniqueMasterDistinctFromAsync(handler.Master,handler.Target,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Invalid_target_never_queries_keycloak()
    {
        using var handler=new MasterHandler();
        Assert.False(await Provider(handler).VerifyUniqueMasterDistinctFromAsync(
            handler.Master,"bad-id",TestContext.Current.CancellationToken));
        Assert.Empty(handler.Paths);
    }

    [Fact]
    public async Task Signed_role_and_username_cannot_substitute_for_actual_master_subject()
    {
        using var handler=new MasterHandler();
        var wrongActor=Guid.NewGuid().ToString();
        Assert.False(await Provider(handler).VerifyUniqueMasterDistinctFromAsync(
            wrongActor,handler.Target,TestContext.Current.CancellationToken));
        // La consulta puede devolver admin con rol, pero su UUID no coincide
        // con el sub de quien solicita la mutación.
        Assert.Single(handler.Paths);
        Assert.DoesNotContain(handler.Paths,p=>p.Contains("/role-mappings/",StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_or_invalid_actor_fails_closed_without_querying_keycloak()
    {
        using var handler=new MasterHandler();
        Assert.False(await Provider(handler).VerifyUniqueMasterDistinctFromAsync(
            "not-a-guid",handler.Target,TestContext.Current.CancellationToken));
        Assert.False(await Provider(handler).VerifyUniqueMasterDistinctFromAsync(
            handler.Target,handler.Target,TestContext.Current.CancellationToken));
        Assert.Empty(handler.Paths);
    }

    private sealed class MasterHandler:HttpMessageHandler
    {
        public string Target{get;}=Guid.NewGuid().ToString();
        public string Master{get;}=Guid.NewGuid().ToString();
        public bool Missing,Disabled,SameSubject,NoMasterRole,WrongUsername,Unavailable;
        public readonly List<string> Paths=[],Methods=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.ToString();
            if(path.EndsWith("/token",StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){
                    Content=JsonContent.Create(new {access_token="synthetic-token"})});
            Methods.Add(request.Method.Method);Paths.Add(path);
            if(Unavailable && path.Contains("/role-mappings/",StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            object body;
            if(path.Contains("/role-mappings/",StringComparison.Ordinal))
                body=NoMasterRole?Array.Empty<object>():new object[]{new{name="platform_superadmin"}};
            else
                body=Missing?Array.Empty<object>():new object[]{new {
                    id=SameSubject?Target:Master,username=WrongUsername?"qa.admin":"admin",enabled=!Disabled
                }};
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(body)});
        }
    }
}
