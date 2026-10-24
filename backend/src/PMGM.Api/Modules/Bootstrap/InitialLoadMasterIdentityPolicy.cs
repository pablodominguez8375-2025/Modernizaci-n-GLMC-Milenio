using System.Security.Claims;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Pure identity match for the unique initial-load Master account. The future
/// caller MUST supply the issuer from a cryptographically validated JWT token
/// and the registered Master identity from trusted SERVER configuration.
/// Never obtain those values from request bodies, headers or user profiles.
/// This policy alone does not grant any import/transition capability.
/// </summary>
public static class InitialLoadMasterIdentityPolicy
{
    public sealed record TrustedMasterRegistration(
        string? Subject,
        string? Issuer);

    public sealed record Result(bool Verified, string Code);

    public static Result Verify(
        ClaimsPrincipal? principal,
        IInstitutionalAccessService? access,
        string? validatedTokenIssuer,
        TrustedMasterRegistration? registration)
    {
        static Result Deny(string code) => new(false, code);

        // Do not allow an unregistered or malformed server identity to match
        // even if the user supplies an admin role and preferred_username.
        if (registration is null ||
            !ValidSubject(registration.Subject) ||
            !ValidIssuer(registration.Issuer))
            return Deny("MASTER_REGISTRATION_UNAVAILABLE");

        if (principal?.Identity?.IsAuthenticated != true || access is null)
            return Deny("AUTHENTICATION_REQUIRED");

        // Support impersonation must never borrow operator Master powers.
        if (principal.HasClaim(c => c.Type == "pmgm_support_operator"))
            return Deny("IMPERSONATION_DENIED");

        if (!access.IsPlatformSuperAdmin(principal))
            return Deny("MASTER_ROLE_REQUIRED");

        // A name/email match is deliberately not sufficient.
        if (string.IsNullOrWhiteSpace(validatedTokenIssuer) ||
            !string.Equals(validatedTokenIssuer, registration.Issuer, StringComparison.Ordinal))
            return Deny("MASTER_ISSUER_MISMATCH");

        var subjects = principal.FindAll("sub").Select(x => x.Value).ToArray();
        var names = principal.FindAll("preferred_username").Select(x => x.Value).ToArray();

        if (subjects.Length != 1 || names.Length != 1 ||
            !string.Equals(subjects[0], registration.Subject, StringComparison.Ordinal) ||
            !string.Equals(names[0], "admin", StringComparison.Ordinal))
            return Deny("MASTER_ACCOUNT_REQUIRED");

        return new Result(true, "MASTER_VERIFIED");
    }

    private static bool ValidSubject(string? subject)
        => !string.IsNullOrWhiteSpace(subject) && subject.Length <= 320 &&
           subject.All(c => !char.IsControl(c) && !char.IsWhiteSpace(c));

    private static bool ValidIssuer(string? issuer)
        => !string.IsNullOrWhiteSpace(issuer) && issuer.Length <= 2048 &&
           issuer.All(c => !char.IsControl(c) && !char.IsWhiteSpace(c)) &&
           Uri.TryCreate(issuer, UriKind.Absolute, out var uri) &&
           uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);
}
