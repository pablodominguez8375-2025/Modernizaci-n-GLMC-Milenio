using System.Net.Mail;
using System.Security.Cryptography;

namespace PMGM.Api.Modules.UserAccounts;

public sealed record CreateUserAccountRequest(Guid? MemberId, Guid? OrganizationId,
    bool PlatformAdministrator = false, string? PlatformName = null, string? PlatformEmail = null,
    bool Superuser = false);
public sealed record AccountCandidate(Guid MemberId, Guid OrganizationId, string Name, string Email, string Workshop);
/// <summary>
/// <c>PlatformAdministrator</c> (rol global Máster) NUNCA se concede desde el alta
/// común: existe una sola cuenta Máster, provista fuera de esta API. El superusuario
/// es otra cosa: cuenta de soporte sin rol global, con perfiles limitados.
/// </summary>
public sealed record AccountIdentity(string Name, string Email, Guid? MemberId, Guid? OrganizationId, bool PlatformAdministrator, bool Superuser = false);
public sealed record PreparedIdentity(string Subject, bool Created);
/// <summary>Identidad efectiva de una cuenta gestionada, tal como la verá el token: roles, alcance y Talleres.</summary>
public sealed record AccountClaims(string Subject, string Name, string Kind, bool Enabled, string[] Roles, string? Scope, string[] Organizations);
public sealed record AccountUser(string Subject, string Name, string Email, bool Enabled, string Kind);

// Inventario de solo lectura de Keycloak, separado de la lista de cuentas gestionadas.
// Nunca concede automáticamente derecho a activar/desactivar/borrar una cuenta heredada.
public sealed record AccountInventoryItem(string Subject, string Username, string Name, bool Enabled,
    string Kind, bool Managed, bool Protected, string Origin);

public interface IAccountIdentityInventory
{
    Task<IReadOnlyList<AccountInventoryItem>> ListInventoryAsync(CancellationToken ct);
}

public interface IAccountIdentityProvider
{
    bool Configured { get; }
    string Issuer { get; }
    Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account, string temporaryPassword, CancellationToken ct);
    Task EnableAsync(string subject, CancellationToken ct);
    Task RemoveAsync(string subject, CancellationToken ct);
    Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct);
    /// <summary>Cuenta gestionada por la plataforma o null si no existe / no es gestionada.</summary>
    Task<AccountUser?> FindManagedAsync(string subject, CancellationToken ct);
    /// <summary>Roles efectivos, alcance y Talleres de una cuenta gestionada (para «Ver como»); null si no existe o no es gestionada.</summary>
    Task<AccountClaims?> GetClaimsAsync(string subject, CancellationToken ct);
    /// <summary>Modalidad A: el proveedor envía un enlace de un solo uso (UPDATE_PASSWORD) al correo de la cuenta.</summary>
    Task SendPasswordRecoveryEmailAsync(string subject, CancellationToken ct);
    /// <summary>Modalidad B: clave temporal con cambio obligatorio; cierra las sesiones activas.</summary>
    Task SetTemporaryPasswordAsync(string subject, string temporaryPassword, CancellationToken ct);
}
public sealed record PasswordRecoveryRequest(string? Modality, string? Reason);
public interface IInitialAccountMail
{
    bool Configured { get; }
    Task SendAsync(AccountIdentity account, string temporaryPassword, CancellationToken ct);
}
public sealed class AccountDeliveryException : Exception { }
public sealed class AccountConflictException : Exception { }

public static class AccountPassword
{
    public static string Generate()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var characters = new char[24];
        characters[0] = 'A'; characters[1] = 'a'; characters[2] = '7'; characters[3] = '!';
        for (var i = 4; i < characters.Length; i++) characters[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        RandomNumberGenerator.Shuffle(characters.AsSpan());
        return new string(characters);
    }
    public static bool ValidEmail(string? value)
        => value is { Length: > 0 and <= 320 } && value == value.Trim() &&
           MailAddress.TryCreate(value, out var mail) && mail.Address == value && value.Contains('@');
}

public sealed class AccountIdentityOptions
{
    public string ServerUrl { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Realm { get; set; } = "pmgm";
    public string ClientId { get; set; } = "pmgm-account-admin";
    public string ClientSecret { get; set; } = "";
}
public sealed class InitialAccountMailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string LoginUrl { get; set; } = "";
}
