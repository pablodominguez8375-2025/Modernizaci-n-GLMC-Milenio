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
        Assert.Equal(4,handler.Calls);
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
        Assert.Equal(7,handler.Calls);
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
    public async Task Old_token_cannot_regain_access_when_user_is_reenabled_with_new_session()
    {
        using var handler=new IntrospectionHandler();
        var provider=Provider(handler);
        var ct=TestContext.Current.CancellationToken;
        Assert.True(await provider.IsActiveAsync("synthetic-bearer",handler.Subject,ct));
        handler.Enabled=false;
        Assert.False(await provider.IsActiveAsync("synthetic-bearer",handler.Subject,ct));
        // The old access token still introspects as active=true; Keycloak
        // has ended the old session and a later login starts a new session.
        handler.Enabled=true;
        handler.TokenSessionId=handler.SessionId;
        handler.SessionId=Guid.NewGuid().ToString();
        Assert.False(await provider.IsActiveAsync("synthetic-bearer",handler.Subject,ct));
        handler.TokenSessionId=handler.SessionId; // fresh token from the new session
        Assert.True(await provider.IsActiveAsync("synthetic-fresh",handler.Subject,ct));
        Assert.Equal(4,handler.IntrospectionCalls);
    }

    [Theory]
    [InlineData(false,false,false,false,false)]
    [InlineData(true,false,false,false,false)]
    [InlineData(false,true,false,false,false)]
    [InlineData(false,false,true,false,false)]
    [InlineData(false,false,false,true,false)]
    [InlineData(false,false,false,false,true)]
    public async Task Absent_or_inconsistent_session_proof_fails_closed(
        bool missingSession,bool conflictingFields,bool invalidSessions,
        bool forbiddenSessions,bool malformedSessionId)
    {
        using var handler=new IntrospectionHandler {
            MissingSession=missingSession,
            ConflictingSessionEvidence=conflictingFields,
            InvalidSessionsResponse=invalidSessions,
            ForbiddenSessions=forbiddenSessions,
            MalformedSessionId=malformedSessionId
        };
        Assert.False(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Session_state_claim_only_is_accepted_when_active_session_matches()
    {
        using var handler=new IntrospectionHandler {SessionStateOnly=true};
        Assert.True(await Provider(handler).IsActiveAsync(
            "synthetic-bearer",handler.Subject,TestContext.Current.CancellationToken));
        handler.SessionId=Guid.NewGuid().ToString();
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
        public string SessionId{get;set;}=Guid.NewGuid().ToString();
        public string? TokenSessionId;
        public bool Active=true,Enabled=true,WrongSubject,Unavailable,Malformed,
            MissingUser,WrongUserId,MissingEnabled,ForbiddenRead,ServiceUnavailable,
            MissingSession,SessionStateOnly,ConflictingSessionEvidence,ForbiddenSessions,
            InvalidSessionsResponse,MalformedSessionId;
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
                var data=new Dictionary<string,object?> {
                    ["active"]=Active,
                    ["sub"]=WrongSubject?Guid.NewGuid().ToString():Subject
                };
                if(!MissingSession)
                {
                    data[SessionStateOnly?"session_state":"sid"]=
                        MalformedSessionId?"invalid-session":TokenSessionId??SessionId;
                    if(ConflictingSessionEvidence)
                        data["session_state"]=Guid.NewGuid().ToString();
                }
                return new(HttpStatusCode.OK) {Content=JsonContent.Create(data)};
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
            if(path.EndsWith("/sessions",StringComparison.Ordinal))
            {
                Assert.Equal("/admin/realms/pmgm/users/"+Subject+"/sessions",path);
                Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);
                Assert.Equal("synthetic-service-token",request.Headers.Authorization?.Parameter);
                if(ForbiddenSessions) return new(HttpStatusCode.Forbidden);
                if(InvalidSessionsResponse) return new(HttpStatusCode.OK) {
                    Content=JsonContent.Create(new {unexpected=true})};
                return new(HttpStatusCode.OK) {
                    Content=JsonContent.Create(new[]{new{id=SessionId}})};
            }
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
