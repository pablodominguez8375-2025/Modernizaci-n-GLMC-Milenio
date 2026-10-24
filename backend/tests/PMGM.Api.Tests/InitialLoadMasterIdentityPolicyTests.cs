using System.Security.Claims;
using System.Text.Json;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadMasterIdentityPolicyTests
{
    private static readonly InstitutionalAccessService Access = new();
    private const string Issuer = "https://keycloak.example.invalid/realms/pmgm-qa";
    private const string MasterSubject = "synthetic-master-opaque-subject";
    private static readonly InitialLoadMasterIdentityPolicy.TrustedMasterRegistration
        Registration = new(MasterSubject, Issuer);

    private static ClaimsPrincipal Principal(
        string subject = MasterSubject,
        string username = "admin",
        string role = InstitutionalRoles.PlatformSuperAdmin,
        string scope = "order",
        bool authenticated = true,
        bool impersonating = false)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("preferred_username", username),
            new("email", "synthetic-admin@example.invalid"),
            new(InstitutionalClaims.Role, role),
            new(InstitutionalClaims.Scope, scope)
        };
        if (impersonating)
            claims.Add(new Claim("pmgm_support_operator", "synthetic-support"));
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims, authenticated ? "validated-jwt-synthetic" : null));
    }

    private static InitialLoadMasterIdentityPolicy.Result Verify(
        ClaimsPrincipal? principal,
        string? issuer = Issuer,
        InitialLoadMasterIdentityPolicy.TrustedMasterRegistration? config = null)
        => InitialLoadMasterIdentityPolicy.Verify(
            principal, Access, issuer, config ?? Registration);

    [Fact]
    public void Registered_identity_with_authenticated_role_scope_and_issuer_matches()
    {
        var result = Verify(Principal());
        Assert.True(result.Verified);
        Assert.Equal("MASTER_VERIFIED", result.Code);
    }

    [Theory]
    [InlineData("wrong-subject", "admin", "MASTER_ACCOUNT_REQUIRED")]
    [InlineData(MasterSubject, "someone-else", "MASTER_ACCOUNT_REQUIRED")]
    [InlineData(MasterSubject, "ADMIN", "MASTER_ACCOUNT_REQUIRED")]
    public void Matching_username_or_role_alone_never_suffices(
        string subject, string username, string code)
    {
        var result = Verify(Principal(subject: subject, username: username));
        Assert.False(result.Verified);
        Assert.Equal(code, result.Code);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(subject, json);
        Assert.DoesNotContain(username, json);
    }

    [Theory]
    [InlineData(InstitutionalRoles.TallerAdmin)]
    [InlineData(InstitutionalRoles.GranSecretaria)]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    public void Institutional_roles_are_not_master_accounts(string role)
    {
        var result = Verify(Principal(role: role));
        Assert.False(result.Verified);
        Assert.Equal("MASTER_ROLE_REQUIRED", result.Code);
    }

    [Fact]
    public void Missing_order_scope_or_authentication_is_denied()
    {
        var result = Verify(Principal(scope: "lodge"));
        Assert.Equal("MASTER_ROLE_REQUIRED", result.Code);
        Assert.False(result.Verified);

        result = Verify(Principal(authenticated: false));
        Assert.Equal("AUTHENTICATION_REQUIRED", result.Code);
        Assert.False(result.Verified);

        result = Verify(null);
        Assert.Equal("AUTHENTICATION_REQUIRED", result.Code);
    }

    [Fact]
    public void Support_impersonation_is_denied_even_if_operator_is_master()
    {
        var result = Verify(Principal(impersonating: true));
        Assert.Equal("IMPERSONATION_DENIED", result.Code);
        Assert.False(result.Verified);
    }

    [Fact]
    public void Validated_token_issuer_is_compared_exactly_and_never_echoed()
    {
        var result = Verify(Principal(), "https://other.example.invalid/realms/pmgm");
        Assert.False(result.Verified);
        Assert.Equal("MASTER_ISSUER_MISMATCH", result.Code);
        Assert.DoesNotContain("other.example", JsonSerializer.Serialize(result));

        result = Verify(Principal(), null);
        Assert.False(result.Verified);
        Assert.Equal("MASTER_ISSUER_MISMATCH", result.Code);
    }

    [Fact]
    public void Missing_or_insecure_server_master_registration_fails_closed()
    {
        var samples = new[]
        {
            new InitialLoadMasterIdentityPolicy.TrustedMasterRegistration(null, Issuer),
            new InitialLoadMasterIdentityPolicy.TrustedMasterRegistration(" ", Issuer),
            new InitialLoadMasterIdentityPolicy.TrustedMasterRegistration(MasterSubject, null),
            new InitialLoadMasterIdentityPolicy.TrustedMasterRegistration(MasterSubject,
                "http://unsecured.example.invalid/realms/pmgm"),
            new InitialLoadMasterIdentityPolicy.TrustedMasterRegistration(MasterSubject,
                "https://valid.example.invalid/realms/pmgm\nInjected"),
            new InitialLoadMasterIdentityPolicy.TrustedMasterRegistration(MasterSubject + "\t",
                Issuer)
        };
        foreach (var registration in samples)
        {
            var result = Verify(Principal(), config: registration);
            Assert.False(result.Verified);
            Assert.Equal("MASTER_REGISTRATION_UNAVAILABLE", result.Code);
        }
    }

    [Fact]
    public void Duplicate_subject_or_username_claims_are_rejected()
    {
        var p = Principal();
        ((ClaimsIdentity)p.Identity!).AddClaim(new Claim("sub", MasterSubject));
        Assert.Equal("MASTER_ACCOUNT_REQUIRED", Verify(p).Code);

        p = Principal();
        ((ClaimsIdentity)p.Identity!).AddClaim(new Claim("preferred_username", "admin"));
        Assert.Equal("MASTER_ACCOUNT_REQUIRED", Verify(p).Code);
    }

    [Fact]
    public void Jwt_bearer_mapped_name_identifier_is_accepted_only_as_unique_subject()
    {
        var principal = Principal();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(Assert.Single(identity.FindAll("sub")));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, MasterSubject));

        var result = Verify(principal);
        Assert.True(result.Verified);
        Assert.Equal("MASTER_VERIFIED", result.Code);
    }

    [Fact]
    public void Raw_and_mapped_subject_together_or_duplicate_mapped_subject_fail_closed()
    {
        var principal = Principal();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, MasterSubject));
        Assert.Equal("MASTER_ACCOUNT_REQUIRED", Verify(principal).Code);

        identity.RemoveClaim(Assert.Single(identity.FindAll("sub")));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, MasterSubject));
        Assert.Equal("MASTER_ACCOUNT_REQUIRED", Verify(principal).Code);
    }

    [Fact]
    public void Wrong_mapped_subject_cannot_replace_unique_master()
    {
        var principal = Principal();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(Assert.Single(identity.FindAll("sub")));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "someone-else"));
        Assert.False(Verify(principal).Verified);
        Assert.Equal("MASTER_ACCOUNT_REQUIRED", Verify(principal).Code);
    }

    [Fact]
    public void Unrelated_personal_attributes_do_not_appear_in_the_result()
    {
        var result = Verify(Principal(subject: "private-identifier"));
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private-identifier", json);
        Assert.DoesNotContain("synthetic-admin@example.invalid", json);
        Assert.DoesNotContain(Issuer, json);
        Assert.DoesNotContain(MasterSubject, json);
    }
}
