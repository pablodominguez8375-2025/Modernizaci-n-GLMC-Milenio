using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PMGM.Api.Modules.UserAccounts;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class AccountTokenIntrospectionTests
{
    private static KeycloakAccountTokenIntrospector Provider(HttpMessageHandler handler)
        => new(new HttpClient(handler),Options.Create(new AccountIdentityOptions {
            ServerUrl="https://identity.example.invalid",
            Issuer="https://identity.example.invalid/realms/pmgm",
            Realm="pmgm",
            ClientId="pmgm-account-admin",ClientSecret="synthetic-only"
        }));

    [Fact]
    public async Task Only_active_token_and_enabled_identity_with_identical_uuid_pass()
    {
        using var handler=new IntrospectionHandler();
        Assert.True(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
        Assert.Equal(3,handler.Calls);
        Assert.Equal(1,handler.IntrospectionCalls);
        Assert.DoesNotContain("synthetic-bearer",string.Join(" ",handler.Paths));
        Assert.Contains(handler.Paths,p=>p.EndsWith("/protocol/openid-connect/token/introspect",StringComparison.Ordinal));
        Assert.Contains(handler.Paths,p=>p.EndsWith("/admin/realms/pmgm/users/"+handler.Subject,StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,true)]
    public async Task Revoked_or_wrong_subject_is_always_denied(bool wrongSubject,bool active)
    {
        using var handler=new IntrospectionHandler { WrongSubject=wrongSubject,Active=active };
        if(!wrongSubject) handler.Active=false;
        Assert.False(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
        Assert.Equal(1,handler.Calls); // no query about the user when introspection fails
    }

    [Fact]
    public async Task Disabled_identity_rejects_still_active_jwt()
    {
        using var handler=new IntrospectionHandler { Enabled=false };
        Assert.False(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
        Assert.Equal(1,handler.IntrospectionCalls);
        Assert.Equal(3,handler.Calls);
    }

    [Fact]
    public async Task Repeated_checks_have_no_cache_and_notice_disable()
    {
        using var handler=new IntrospectionHandler();
        var provider=Provider(handler);
        Assert.True(await provider.IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
        handler.Enabled=false; // token remains active; identity has been blocked
        Assert.False(await provider.IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
        Assert.Equal(2,handler.IntrospectionCalls);
        Assert.Equal(6,handler.Calls);
    }

    [Fact]
    public async Task Provider_failure_or_invalid_response_fails_closed()
    {
        using var handler=new IntrospectionHandler { Unavailable=true };
        Assert.False(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
        handler.Unavailable=false;handler.Malformed=true;
        Assert.False(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true,false,false,false,false)]
    [InlineData(false,true,false,false,false)]
    [InlineData(false,false,true,false,false)]
    [InlineData(false,false,false,true,false)]
    [InlineData(false,false,false,false,true)]
    public async Task Missing_wrong_or_unverifiable_identity_fails_closed(
        bool missingUser,bool wrongId,bool missingEnabled,bool forbiddenRead,bool serviceUnavailable)
    {
        using var handler=new IntrospectionHandler {
            MissingUser=missingUser,WrongUserId=wrongId,MissingEnabled=missingEnabled,
            ForbiddenRead=forbiddenRead,ServiceUnavailable=serviceUnavailable
        };
        Assert.False(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Invalid_subject_or_token_does_not_call_provider()
    {
        using var handler=new IntrospectionHandler();
        var provider=Provider(handler);
        Assert.False(await provider.IsActiveAsync("synthetic-bearer","not-a-guid",
            TestContext.Current.CancellationToken));
        Assert.False(await provider.IsActiveAsync("",handler.Subject,
            TestContext.Current.CancellationToken));
        Assert.Equal(0,handler.Calls);
    }

    private sealed class IntrospectionHandler:HttpMessageHandler
    {
        public string Subject{get;}=Guid.NewGuid().ToString();
        public bool Active=true,Enabled=true,WrongSubject,Unavailable,Malformed,
            MissingUser,WrongUserId,MissingEnabled,ForbiddenRead,ServiceUnavailable;
        public int Calls,IntrospectionCalls;
        public readonly List<string> Paths=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;var path=request.RequestUri!.AbsolutePath;
            Paths.Add(request.RequestUri.ToString());
            if(path.EndsWith("/introspect",StringComparison.Ordinal))
            {
                IntrospectionCalls++;
                Assert.Equal(HttpMethod.Post,request.Method);
                var body=await request.Content!.ReadAsStringAsync(ct);
                Assert.Contains("token=synthetic-bearer",body,StringComparison.Ordinal);
                Assert.Contains("client_secret=synthetic-only",body,StringComparison.Ordinal);
                if(Unavailable) return new(HttpStatusCode.ServiceUnavailable);
                if(Malformed) return new(HttpStatusCode.OK) {Content=new StringContent("{invalid")};
                return new(HttpStatusCode.OK) {Content=JsonContent.Create(new {
                    active=Active,sub=WrongSubject?Guid.NewGuid().ToString():Subject
                })};
            }
            if(path.EndsWith("/token",StringComparison.Ordinal))
            {
                Assert.Equal(HttpMethod.Post,request.Method);
                if(ServiceUnavailable) return new(HttpStatusCode.ServiceUnavailable);
                return new(HttpStatusCode.OK) {
                    Content=JsonContent.Create(new {access_token="synthetic-service-token"})
                };
            }
            Assert.Equal(HttpMethod.Get,request.Method);
            Assert.EndsWith("/admin/realms/pmgm/users/"+Subject,path,StringComparison.Ordinal);
            Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);
            Assert.Equal("synthetic-service-token",request.Headers.Authorization?.Parameter);
            if(ForbiddenRead) return new(HttpStatusCode.Forbidden);
            if(MissingUser) return new(HttpStatusCode.NotFound);
            if(MissingEnabled) return new(HttpStatusCode.OK) {
                Content=JsonContent.Create(new{id=Subject})};
            return new(HttpStatusCode.OK) {Content=JsonContent.Create(new {
                id=WrongUserId?Guid.NewGuid().ToString():Subject,
                enabled=Enabled
            })};
        }
    }
}
