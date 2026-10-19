using System.Net.Mail;

namespace PMGM.Api.Modules.Privacy;

/// <summary>Correo único, versionado y sin nombres para derechos del titular.</summary>
public static class PrivacyContactPolicy
{
    public const string Code = "system.privacy.contact_email";
    public const string DefaultEmail = "juridico@granlogiamixta.cl";

    public static string? Normalize(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        var value = candidate.Trim();
        if (value.Length > 254 || value.Contains(',') || value.Contains(';') ||
            value.Any(char.IsWhiteSpace) || value.Count(c => c == '@') != 1 ||
            !MailAddress.TryCreate(value, out var address) ||
            !string.Equals(address.Address, value, StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(address.DisplayName) ||
            address.Host.IndexOf('.') < 1)
            return null;
        return value;
    }
}
