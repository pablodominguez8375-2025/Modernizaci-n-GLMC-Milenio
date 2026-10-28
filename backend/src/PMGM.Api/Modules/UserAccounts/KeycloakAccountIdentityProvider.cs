using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PMGM.Api.Modules.UserAccounts;

public sealed class KeycloakAccountIdentityProvider(HttpClient http, IOptions<AccountIdentityOptions> options) : IAccountIdentityProvider, IAccountIdentityInventory
{
    private readonly AccountIdentityOptions settings = options.Value;
    public string Issuer => settings.Issuer.Trim();
    public bool Configured => Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out var server) &&
        server.Scheme is "https" or "http" && Uri.TryCreate(Issuer, UriKind.Absolute, out _) &&
        Issuer.Length <= 500 && !string.IsNullOrWhiteSpace(settings.Realm) && !string.IsNullOrWhiteSpace(settings.ClientSecret);
    private string Admin(string path) => $"{settings.ServerUrl.TrimEnd('/')}/admin/realms/{Uri.EscapeDataString(settings.Realm)}/{path}";
    private async Task<string> Token(CancellationToken ct)
    {
        if (!Configured) throw new AccountDeliveryException();
        using var response = await http.PostAsync($"{settings.ServerUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(settings.Realm)}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"]="client_credentials", ["client_id"]=settings.ClientId, ["client_secret"]=settings.ClientSecret }), ct);
        if (!response.IsSuccessStatusCode) throw new AccountDeliveryException();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("access_token").GetString() ?? throw new AccountDeliveryException();
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Admin(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await Token(ct));
        if (body is not null) request.Content = JsonContent.Create(body);
        return await http.SendAsync(request, ct);
    }
    private static string Attribute(JsonElement user, string name)
        => user.TryGetProperty("attributes",out var a) && a.ValueKind==JsonValueKind.Object && a.TryGetProperty(name,out var v) && v.ValueKind==JsonValueKind.Array && v.GetArrayLength()>0
            ? v.EnumerateArray().FirstOrDefault().GetString() ?? "" : "";
    private static void Success(HttpResponseMessage response)
    {
        if(response.StatusCode==HttpStatusCode.Conflict) throw new AccountConflictException();
        if(!response.IsSuccessStatusCode) throw new AccountDeliveryException();
    }
    public async Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account, string temporaryPassword, CancellationToken ct)
    {
        // Fail-closed: el rol global de Máster jamás se concede por esta vía (regla del PO 22-10).
        if(account.PlatformAdministrator) throw new AccountConflictException();
        string? subject=null; var created=false;
        using(var lookup=await Send(HttpMethod.Get,$"users?username={Uri.EscapeDataString(account.Email.ToLowerInvariant())}&exact=true",null,ct))
        {
            Success(lookup);using var json=JsonDocument.Parse(await lookup.Content.ReadAsStringAsync(ct));
            if(json.RootElement.GetArrayLength()>0)
            {
                var existing=json.RootElement[0];
                if(existing.GetProperty("enabled").GetBoolean() || Attribute(existing,"pmgm_managed")!="active-member-accounts" ||
                   Attribute(existing,"pmgm_member_id")!=(account.MemberId?.ToString()??"") ||
                   Attribute(existing,"pmgm_account_kind")!=(account.Superuser?"superuser":"member") ||
                   !string.Equals(existing.GetProperty("email").GetString(),account.Email,StringComparison.OrdinalIgnoreCase)) throw new AccountConflictException();
                subject=existing.GetProperty("id").GetString();
            }
        }
        var parts=account.Name.Trim().Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var names=parts.Length>1?string.Join(' ',parts[..^1]):account.Name;
        var userRepresentation=new { username=account.Email.ToLowerInvariant(),email=account.Email,
                firstName=names,lastName=parts.Length>1?parts[^1]:"Plataforma",enabled=false,emailVerified=false,
                requiredActions=new[]{"UPDATE_PASSWORD"}, attributes=new Dictionary<string,string[]> {
                    ["pmgm_managed"]=["active-member-accounts"],["pmgm_account_kind"]=[account.Superuser?"superuser":"member"],
                    ["pmgm_member_id"]=[account.MemberId?.ToString()??""],["pmgm_scope"]=[account.Superuser?"order":"organization"],
                    ["pmgm_org"]=[account.OrganizationId?.ToString()??""] } };
        if(subject is null)
        {
            using var response=await Send(HttpMethod.Post,"users",userRepresentation,ct);
            Success(response);
            subject=response.Headers.Location?.Segments.Last().Trim('/');
            if(string.IsNullOrWhiteSpace(subject) || !Guid.TryParse(subject,out _)) throw new AccountDeliveryException();
            created=true;
        }
        try
        {
            // A retry may follow a change of Taller: refresh server-derived claims while still disabled.
            if(!created)
            {
                using var update=await Send(HttpMethod.Put,$"users/{Uri.EscapeDataString(subject)}",userRepresentation,ct);Success(update);
            }
            using var password=await Send(HttpMethod.Put,$"users/{subject}/reset-password",new { type="password",value=temporaryPassword,temporary=true },ct);Success(password);
            return new(subject,created);
        }
        catch
        {
            if(created) { try { await RemoveAsync(subject,CancellationToken.None); } catch { /* New identity remains disabled. */ } }
            throw;
        }
    }
    public async Task EnableAsync(string subject,CancellationToken ct)
    {
        using var response=await Send(HttpMethod.Put,$"users/{Uri.EscapeDataString(subject)}",new{enabled=true},ct);Success(response);
    }
    public async Task RemoveAsync(string subject,CancellationToken ct)
    {
        // Disable first: an uncertain activation response must never leave an orphan active.
        using var disabled=await Send(HttpMethod.Put,$"users/{Uri.EscapeDataString(subject)}",new{enabled=false},ct);
        if(disabled.StatusCode!=HttpStatusCode.NotFound) Success(disabled);
        using var removed=await Send(HttpMethod.Delete,$"users/{Uri.EscapeDataString(subject)}",null,ct);
        if(removed.StatusCode!=HttpStatusCode.NotFound) Success(removed);
    }
    public async Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct)
    {
        if(!Configured) return [];
        var result=new List<AccountUser>();
        for(var first=0;;first+=100)
        {
            using var response=await Send(HttpMethod.Get,$"users?q=pmgm_managed:active-member-accounts&first={first}&max=100",null,ct);Success(response);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            foreach(var x in json.RootElement.EnumerateArray().Where(x=>Attribute(x,"pmgm_managed")=="active-member-accounts"))
                result.Add(new AccountUser(x.GetProperty("id").GetString()!,
                    $"{Text(x,"firstName")} {Text(x,"lastName")}".Trim(),Text(x,"email"),x.GetProperty("enabled").GetBoolean(),Attribute(x,"pmgm_account_kind")));
            if(json.RootElement.GetArrayLength()<100) return result;
        }
    }
    // Inventario master-only: listar TODAS las identidades existentes, incluidas las
    // provisionadas antes del alta. Las cuentas sin marca verificada se muestran
    // como protegidas, nunca se reclasifican silenciosamente ni se modifican.
    public async Task<IReadOnlyList<AccountInventoryItem>> ListInventoryAsync(CancellationToken ct)
    {
        if(!Configured) throw new AccountDeliveryException();
        var result=new List<AccountInventoryItem>();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(var first=0;first<5000;first+=100)
        {
            using var response=await Send(HttpMethod.Get,$"users?first={first}&max=100",null,ct);Success(response);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if(json.RootElement.ValueKind!=JsonValueKind.Array) throw new AccountDeliveryException();
            foreach(var x in json.RootElement.EnumerateArray())
            {
                if(!x.TryGetProperty("id",out var id) || !Guid.TryParse(id.GetString(),out var guid)
                    || !x.TryGetProperty("username",out var usernameField)
                    || usernameField.ValueKind!=JsonValueKind.String
                    || !x.TryGetProperty("enabled",out var enabledField)
                    || enabledField.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new AccountDeliveryException();
                var subject=guid.ToString();
                if(!seen.Add(subject)) throw new AccountDeliveryException();
                var username=usernameField.GetString()??"";
                if(string.IsNullOrWhiteSpace(username)) throw new AccountDeliveryException();
                var accountKind=Attribute(x,"pmgm_account_kind");
                var managed=Attribute(x,"pmgm_managed")=="active-member-accounts"
                    && accountKind is "member" or "superuser";
                var protectedAccount=!managed || string.Equals(username,"admin",StringComparison.OrdinalIgnoreCase);
                var kind=protectedAccount?"legacy_protected":accountKind;
                result.Add(new AccountInventoryItem(subject,username,
                    $"{Text(x,"firstName")} {Text(x,"lastName")}".Trim(),
                    enabledField.GetBoolean(),kind,managed&&!protectedAccount,
                    protectedAccount,protectedAccount?"provisioned":"managed"));
            }
            if(json.RootElement.GetArrayLength()<100) return result;
        }
        // No declarar inventario completo si el realm excede el límite de lectura.
        throw new AccountDeliveryException();
    }
    public async Task<AccountUser?> FindManagedAsync(string subject,CancellationToken ct)
    {
        if(!Guid.TryParse(subject,out var id)) return null;
        using var response=await Send(HttpMethod.Get,$"users/{id}",null,ct);
        if(response.StatusCode==HttpStatusCode.NotFound) return null;
        Success(response);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var x=json.RootElement;
        if(Attribute(x,"pmgm_managed")!="active-member-accounts") return null;
        return new AccountUser(id.ToString(),$"{Text(x,"firstName")} {Text(x,"lastName")}".Trim(),Text(x,"email"),x.GetProperty("enabled").GetBoolean(),Attribute(x,"pmgm_account_kind"));
    }
    public async Task<AccountClaims?> GetClaimsAsync(string subject,CancellationToken ct)
    {
        if(!Guid.TryParse(subject,out var id)) return null;
        using var response=await Send(HttpMethod.Get,$"users/{id}",null,ct);
        if(response.StatusCode==HttpStatusCode.NotFound) return null;
        Success(response);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var x=json.RootElement;
        if(Attribute(x,"pmgm_managed")!="active-member-accounts") return null;
        using var rolesResponse=await Send(HttpMethod.Get,$"users/{id}/role-mappings/realm/composite",null,ct);
        Success(rolesResponse);
        using var roles=JsonDocument.Parse(await rolesResponse.Content.ReadAsStringAsync(ct));
        var names=roles.RootElement.EnumerateArray().Select(r=>r.TryGetProperty("name",out var n)?n.GetString():null).OfType<string>().ToArray();
        var scope=Attribute(x,"pmgm_scope");
        var orgs=x.TryGetProperty("attributes",out var a)&&a.TryGetProperty("pmgm_org",out var o)&&o.ValueKind==JsonValueKind.Array
            ?o.EnumerateArray().Select(v=>v.GetString()).OfType<string>().Where(v=>Guid.TryParse(v,out _)).ToArray():[];
        return new AccountClaims(id.ToString(),$"{Text(x,"firstName")} {Text(x,"lastName")}".Trim(),Attribute(x,"pmgm_account_kind"),
            x.GetProperty("enabled").GetBoolean(),names,string.IsNullOrWhiteSpace(scope)?null:scope,orgs);
    }
    public async Task SendPasswordRecoveryEmailAsync(string subject,CancellationToken ct)
    {
        // Enlace de un solo uso con vigencia corta (30 min); el correo lo envía el proveedor, la API nunca ve la clave.
        using var response=await Send(HttpMethod.Put,$"users/{Uri.EscapeDataString(subject)}/execute-actions-email?lifespan=1800",new[]{"UPDATE_PASSWORD"},ct);
        Success(response);
        await LogoutAsync(subject,ct);
    }
    public async Task SetTemporaryPasswordAsync(string subject,string temporaryPassword,CancellationToken ct)
    {
        using var password=await Send(HttpMethod.Put,$"users/{Uri.EscapeDataString(subject)}/reset-password",new{type="password",value=temporaryPassword,temporary=true},ct);
        Success(password);
        await LogoutAsync(subject,ct);
    }
    private async Task LogoutAsync(string subject,CancellationToken ct)
    {
        using var response=await Send(HttpMethod.Post,$"users/{Uri.EscapeDataString(subject)}/logout",null,ct);
        if(response.StatusCode!=HttpStatusCode.NotFound) Success(response);
    }
    private static string Text(JsonElement user,string field)=>user.TryGetProperty(field,out var value)?value.GetString()??"":"";
}
