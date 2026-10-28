using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PMGM.Api.Modules.UserAccounts;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class AccountIdentityLifecycleCommandTests
{
    private static KeycloakAccountIdentityProvider Provider(HttpMessageHandler handler)
        => new(new HttpClient(handler),Options.Create(new AccountIdentityOptions {
            ServerUrl="https://identity.example.invalid",
            Issuer="https://identity.example.invalid/realms/pmgm",
            ClientSecret="synthetic-test-only"
        }));

    [Fact]
    public async Task Disable_closes_sessions_then_delete_requires_confirmed_disabled_state()
    {
        using var handler=new LifecycleHandler();
        var provider=Provider(handler);
        await provider.DisableAndLogoutAsync(handler.Subject,TestContext.Current.CancellationToken);
        Assert.False(handler.Enabled);
        Assert.Equal(new[]{"PUT","POST"},handler.Methods);
        await provider.DeleteDisabledAsync(handler.Subject,TestContext.Current.CancellationToken);
        Assert.True(handler.Deleted);
        Assert.Equal(new[]{"PUT","POST","GET","DELETE"},handler.Methods);
    }

    [Fact]
    public async Task Delete_never_operates_when_identity_is_still_active()
    {
        using var handler=new LifecycleHandler{Enabled=true};
        await Assert.ThrowsAsync<AccountConflictException>(()=>
            Provider(handler).DeleteDisabledAsync(handler.Subject,TestContext.Current.CancellationToken));
        Assert.False(handler.Deleted);
        Assert.Equal(new[]{"GET"},handler.Methods);
    }

    [Fact]
    public async Task Failed_logout_leaves_account_disabled_and_prevents_delete_in_same_command()
    {
        using var handler=new LifecycleHandler{LogoutFails=true};
        await Assert.ThrowsAsync<AccountDeliveryException>(()=>
            Provider(handler).DisableAndLogoutAsync(handler.Subject,TestContext.Current.CancellationToken));
        Assert.False(handler.Enabled);
        Assert.False(handler.Deleted);
        Assert.Equal(new[]{"PUT","POST"},handler.Methods);
    }

    [Fact]
    public async Task Reactivation_only_calls_enabled_update_on_exact_subject()
    {
        using var handler=new LifecycleHandler{Enabled=false};
        await Provider(handler).ReactivateAsync(handler.Subject,TestContext.Current.CancellationToken);
        Assert.True(handler.Enabled);
        Assert.Equal(new[]{"PUT"},handler.Methods);
        Assert.False(handler.Deleted);
    }

    [Fact]
    public async Task Malformed_subject_cannot_write_to_identity()
    {
        using var handler=new LifecycleHandler();
        await Assert.ThrowsAsync<AccountConflictException>(()=>
            Provider(handler).DeleteDisabledAsync("invalid-id",TestContext.Current.CancellationToken));
        Assert.Empty(handler.Methods);
    }

    private sealed class LifecycleHandler:HttpMessageHandler
    {
        public string Subject{get;}=Guid.NewGuid().ToString();
        public bool Enabled=true,Deleted,LogoutFails;
        public readonly List<string> Methods=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/token",StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content=JsonContent.Create(new {access_token="synthetic-only"})};
            Methods.Add(request.Method.Method);
            if(request.Method==HttpMethod.Put)
            {
                // El único PUT del test es el cambio de estado.
                var body=await request.Content!.ReadAsStringAsync(ct);
                Enabled=body.Contains("\"enabled\":true",StringComparison.OrdinalIgnoreCase);
            }
            if(request.Method==HttpMethod.Post && path.EndsWith("/logout",StringComparison.Ordinal)
                && LogoutFails)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            if(request.Method==HttpMethod.Delete) Deleted=true;
            if(request.Method==HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content=JsonContent.Create(new {id=Subject,enabled=Enabled})};
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
