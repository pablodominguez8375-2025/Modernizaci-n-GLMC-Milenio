using Xunit;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Tests.Authorization;

public sealed class AccountIdentityProviderTests
{
    [Fact]
    public void Passwords_are_random_long_and_have_required_character_classes()
    {
        var passwords=Enumerable.Range(0,100).Select(_=>AccountPassword.Generate()).ToArray();
        Assert.Equal(100,passwords.Distinct().Count());
        foreach(var p in passwords){Assert.Equal(24,p.Length);Assert.Contains(p,char.IsUpper);Assert.Contains(p,char.IsLower);Assert.Contains(p,char.IsDigit);Assert.Contains(p,c=>!char.IsLetterOrDigit(c));}
    }
    [Theory]
    [InlineData("nombre@example.invalid",true)]
    [InlineData("Nombre <nombre@example.invalid>",false)]
    [InlineData("nombre@example.invalid\r\nBcc: otro@example.invalid",false)]
    [InlineData("sin correo",false)]
    public void Delivery_requires_only_the_registered_address(string value,bool valid)=>Assert.Equal(valid,AccountPassword.ValidEmail(value));

    [Fact]
    public async Task Keycloak_is_prepared_disabled_with_temporary_password_and_no_institutional_roles()
    {
        var handler=new Handler();var provider=new KeycloakAccountIdentityProvider(new HttpClient(handler),Options.Create(new AccountIdentityOptions{
            ServerUrl="https://identity.example.invalid",Issuer="https://identity.example.invalid/realms/pmgm",ClientSecret="synthetic-test-only"}));
        var member=Guid.NewGuid();var org=Guid.NewGuid();var password=AccountPassword.Generate();
        var prepared=await provider.PrepareDisabledAsync(new("Hermano Ficticio","miembro@example.invalid",member,org,false),password,TestContext.Current.CancellationToken);
        Assert.True(prepared.Created);Assert.Equal(handler.Subject,prepared.Subject);
        using var user=JsonDocument.Parse(handler.Bodies.Single(x=>x.Path.EndsWith("/users")).Body);
        Assert.False(user.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal("UPDATE_PASSWORD",user.RootElement.GetProperty("requiredActions")[0].GetString());
        Assert.Equal(member.ToString(),user.RootElement.GetProperty("attributes").GetProperty("pmgm_member_id")[0].GetString());
        using var credential=JsonDocument.Parse(handler.Bodies.Single(x=>x.Path.EndsWith("/reset-password")).Body);
        Assert.True(credential.RootElement.GetProperty("temporary").GetBoolean());
        Assert.Equal(password,credential.RootElement.GetProperty("value").GetString());
        Assert.DoesNotContain(handler.Bodies,x=>x.Path.Contains("role-mappings"));
        await provider.EnableAsync(prepared.Subject,TestContext.Current.CancellationToken);
        await provider.RemoveAsync(prepared.Subject,TestContext.Current.CancellationToken);
        Assert.Equal("DELETE",handler.Methods.Last());
    }
    // Regla del PO (22-10): el rol global Máster jamás se concede desde esta vía; el superusuario no lo recibe.
    [Fact]
    public async Task Master_role_is_never_granted_and_superuser_gets_order_scope_without_global_role()
    {
        var handler=new Handler();var provider=new KeycloakAccountIdentityProvider(new HttpClient(handler),Options.Create(new AccountIdentityOptions{
            ServerUrl="https://identity.example.invalid",Issuer="https://identity.example.invalid/realms/pmgm",ClientSecret="synthetic-test-only"}));
        await Assert.ThrowsAsync<AccountConflictException>(()=>provider.PrepareDisabledAsync(new("Otro Master","master@example.invalid",null,null,true),AccountPassword.Generate(),TestContext.Current.CancellationToken));
        Assert.Empty(handler.Bodies);
        await provider.PrepareDisabledAsync(new("Soporte Ficticio","soporte@example.invalid",null,null,false,true),AccountPassword.Generate(),TestContext.Current.CancellationToken);
        Assert.DoesNotContain(handler.Bodies,x=>x.Path.Contains("role-mappings"));
        using var user=JsonDocument.Parse(handler.Bodies.Single(x=>x.Path.EndsWith("/users")).Body);
        var attributes=user.RootElement.GetProperty("attributes");
        Assert.Equal("order",attributes.GetProperty("pmgm_scope")[0].GetString());
        Assert.Equal("superuser",attributes.GetProperty("pmgm_account_kind")[0].GetString());
    }
    private sealed class Handler:HttpMessageHandler
    {
        public readonly string Subject=Guid.NewGuid().ToString();public List<(string Path,string Body)> Bodies=[];public List<string> Methods=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.AbsolutePath;Methods.Add(request.Method.Method);
            if(path.EndsWith("/token"))return new(HttpStatusCode.OK){Content=new StringContent("{\"access_token\":\"synthetic-token\"}")};
            if(request.Content is not null)Bodies.Add((path,await request.Content.ReadAsStringAsync(ct)));
            if(path.EndsWith("/roles/platform_superadmin"))return new(HttpStatusCode.OK){Content=new StringContent("{\"id\":\"synthetic-role\",\"name\":\"platform_superadmin\"}")};
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent("[]")};
            if(request.Method==HttpMethod.Post){var r=new HttpResponseMessage(HttpStatusCode.Created);r.Headers.Location=new Uri("https://identity.example.invalid/admin/realms/pmgm/users/"+Subject);return r;}
            return new(HttpStatusCode.NoContent);
        }
    }
}
