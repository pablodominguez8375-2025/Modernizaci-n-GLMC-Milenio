using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PMGM.Api.Modules.UserAccounts;

/// <summary>
/// Validación de tokens en vivo sin caché para operaciones de cuentas (#768).
/// Las firmas JWT locales no revocan un access token al cerrar la sesión.
/// Solo se habilita con configuración de identidad confidencial explícita.
/// </summary>
public interface IAccountTokenIntrospector
{
    Task<bool> IsActiveAsync(string token, string subject, CancellationToken ct);
}

public sealed class KeycloakAccountTokenIntrospector(HttpClient http,
    IOptions<AccountIdentityOptions> options) : IAccountTokenIntrospector
{
    private readonly AccountIdentityOptions settings = options.Value;

    public async Task<bool> IsActiveAsync(string token, string subject, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 65536 ||
            !Guid.TryParse(subject,out var expected) ||
            !Uri.TryCreate(settings.ServerUrl,UriKind.Absolute,out var server) ||
            server.Scheme is not ("https" or "http") ||
            string.IsNullOrWhiteSpace(settings.ClientId) ||
            string.IsNullOrWhiteSpace(settings.ClientSecret) ||
            string.IsNullOrWhiteSpace(settings.Realm)) return false;

        try
        {
            var url=$"{settings.ServerUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(settings.Realm)}/protocol/openid-connect/token/introspect";
            using var form=new FormUrlEncodedContent(new Dictionary<string,string> {
                ["client_id"]=settings.ClientId,["client_secret"]=settings.ClientSecret,
                ["token"]=token,["token_type_hint"]="access_token"
            });
            using var response=await http.PostAsync(url,form,ct);
            if(!response.IsSuccessStatusCode) return false;
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root=json.RootElement;
            if(root.ValueKind!=JsonValueKind.Object ||
               !root.TryGetProperty("active",out var active) || active.ValueKind!=JsonValueKind.True ||
               !root.TryGetProperty("sub",out var sub) || sub.ValueKind!=JsonValueKind.String ||
               !Guid.TryParse(sub.GetString(),out var actual) || actual!=expected ||
               !TryReadSessionId(root,out var tokenSessionId))
                return false;
            // La introspección puede seguir marcando activo un access token
            // previamente emitido. Comprobar además la identidad en Keycloak,
            // por UUID exacto, en cada solicitud: cuentas desactivadas o borradas
            // quedan bloqueadas aun si el token aún no expiró.
            using var credentials=new FormUrlEncodedContent(new Dictionary<string,string> {
                ["grant_type"]="client_credentials",["client_id"]=settings.ClientId,
                ["client_secret"]=settings.ClientSecret
            });
            var baseUrl=settings.ServerUrl.TrimEnd('/');
            var realm=Uri.EscapeDataString(settings.Realm);
            using var serviceResponse=await http.PostAsync(
                $"{baseUrl}/realms/{realm}/protocol/openid-connect/token",credentials,ct);
            if(!serviceResponse.IsSuccessStatusCode) return false;
            using var serviceToken=JsonDocument.Parse(
                await serviceResponse.Content.ReadAsStringAsync(ct));
            if(serviceToken.RootElement.ValueKind!=JsonValueKind.Object ||
                !serviceToken.RootElement.TryGetProperty("access_token",out var access) ||
                access.ValueKind!=JsonValueKind.String ||
                string.IsNullOrWhiteSpace(access.GetString())) return false;

            using var userRequest=new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/admin/realms/{realm}/users/{expected}");
            userRequest.Headers.Authorization=new AuthenticationHeaderValue(
                "Bearer",access.GetString());
            using var userResponse=await http.SendAsync(userRequest,ct);
            if(!userResponse.IsSuccessStatusCode) return false;
            using var user=JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(ct));
            var entry=user.RootElement;
            if(entry.ValueKind!=JsonValueKind.Object ||
                !entry.TryGetProperty("id",out var uid) ||
                uid.ValueKind!=JsonValueKind.String ||
                !Guid.TryParse(uid.GetString(),out var found) || found!=expected ||
                !entry.TryGetProperty("enabled",out var enabled) ||
                enabled.ValueKind!=JsonValueKind.True) return false;
            // Tras un logout el access token puede seguir figurando active=true.
            // Si la cuenta se reactiva, enabled=true tampoco prueba que la
            // sesión ORIGINAL de ese token continúe abierta. Solo se admite
            // el token si la sesión exacta sigue registrada para este usuario.
            using var sessionsRequest=new HttpRequestMessage(HttpMethod.Get,
                $"{baseUrl}/admin/realms/{realm}/users/{expected}/sessions");
            sessionsRequest.Headers.Authorization=new AuthenticationHeaderValue(
                "Bearer",access.GetString());
            using var sessionsResponse=await http.SendAsync(sessionsRequest,ct);
            if(!sessionsResponse.IsSuccessStatusCode) return false;
            using var sessions=JsonDocument.Parse(await sessionsResponse.Content.ReadAsStringAsync(ct));
            if(sessions.RootElement.ValueKind!=JsonValueKind.Array) return false;
            return sessions.RootElement.EnumerateArray().Any(session=>
                session.ValueKind==JsonValueKind.Object &&
                session.TryGetProperty("id",out var id) &&
                id.ValueKind==JsonValueKind.String &&
                string.Equals(id.GetString(),tokenSessionId,StringComparison.Ordinal));
        }
        catch (Exception e) when(e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        { return false; }
    }

    // Keycloak puede exponer sid o session_state según cliente/protocolo.
    // Si ambos existen deben coincidir; ausencia, tipo inesperado o
    // identificadores incompatibles son evidencia insuficiente (fail closed).
    private static bool TryReadSessionId(JsonElement root,out string sessionId)
    {
        sessionId="";
        var found=false;
        foreach(var name in new[]{"sid","session_state"})
        {
            if(!root.TryGetProperty(name,out var value) ||
               value.ValueKind==JsonValueKind.Null) continue;
            if(value.ValueKind!=JsonValueKind.String) return false;
            var candidate=value.GetString();
            // OIDC sid es un identificador opaco, no necesariamente UUID.
            if(string.IsNullOrWhiteSpace(candidate) || candidate.Length>256 ||
               candidate.Any(char.IsControl)) return false;
            if(found && !string.Equals(sessionId,candidate,StringComparison.Ordinal))
                return false;
            sessionId=candidate;
            found=true;
        }
        return found;
    }
}
