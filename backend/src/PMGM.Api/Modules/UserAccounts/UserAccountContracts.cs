using System.Net.Mail;
using System.Security.Cryptography;

namespace PMGM.Api.Modules.UserAccounts;

public sealed record CreateUserAccountRequest(Guid? MemberId, Guid? OrganizationId,
    bool PlatformAdministrator = false, string? PlatformName = null, string? PlatformEmail = null);
public sealed record AccountCandidate(Guid MemberId, Guid OrganizationId, string Name, string Email, string Workshop);
public sealed record AccountIdentity(string Name, string Email, Guid? MemberId, Guid? OrganizationId, bool PlatformAdministrator);
public sealed record PreparedIdentity(string Subject, bool Created);
public sealed record AccountUser(string Subject, string Name, string Email, bool Enabled, string Kind);

public interface IAccountIdentityProvider
{
    bool Configured { get; }
    string Issuer { get; }
    Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account, string temporaryPassword, CancellationToken ct);
    Task EnableAsync(string subject, CancellationToken ct);
    Task RemoveAsync(string subject, CancellationToken ct);
    Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct);
}
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
