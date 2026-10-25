using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadWindowStatusEndpointsTests
{
    private const string Issuer = "https://kc.example.invalid/realms/centenario";
    private const string Subject = "master-synthetic-subject";
    private static readonly InstitutionalAccessService Access = new();

    private static IConfiguration Config(
        string? subject = Subject,
        string? issuer = Issuer,
        string? authority = Issuer)
        => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = authority,
                ["InitialLoad:MasterSubject"] = subject,
                ["InitialLoad:MasterIssuer"] = issuer
            }).Build();

    private static ClaimsPrincipal Principal(
        string subject = Subject, string issuer = Issuer,
        string username = "admin", string role = InstitutionalRoles.PlatformSuperAdmin,
        string scope = "order", bool authenticated = true,
        bool impersonation = false)
    {
        var claims = new List<Claim>
        {
            new("iss", issuer),
            new("sub", subject),
            new("preferred_username", username),
            new(InstitutionalClaims.Role, role),
            new(InstitutionalClaims.Scope, scope),
            new("email", "not-used@example.invalid")
        };
        if (impersonation)
            claims.Add(new Claim("pmgm_support_operator", "fake-support"));
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims, authenticated ? "test-validated-bearer" : null));
    }

    [Fact]
    public void Authenticated_registered_master_may_read_state_but_never_apply()
    {
        Assert.True(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(), Access, Config()));
        var status = InitialLoadWindowStatusReader.Project(
            new PMGM.Api.Modules.Bootstrap.Entities.InitialLoadWindow());
        Assert.False(status.CanApply);
        Assert.Equal(0, status.WritesPerformed);
    }

    [Fact]
    public void Validated_bearer_with_mapped_subject_remains_master_only()
    {
        var principal = Principal();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(Assert.Single(identity.FindAll("sub")));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Subject));
        Assert.True(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            principal, Access, Config()));

        identity.AddClaim(new Claim("sub", Subject));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            principal, Access, Config()));
    }

    [Fact]
    public void No_server_master_registration_is_denied_by_default()
    {
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(), Access, new ConfigurationBuilder().Build()));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(), Access, Config(subject: null)));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(), Access, Config(issuer: null)));
    }

    [Theory]
    [InlineData("wrong-subject", Issuer, "admin", InstitutionalRoles.PlatformSuperAdmin, "order")]
    [InlineData(Subject, Issuer, "other", InstitutionalRoles.PlatformSuperAdmin, "order")]
    [InlineData(Subject, Issuer, "ADMIN", InstitutionalRoles.PlatformSuperAdmin, "order")]
    [InlineData(Subject, Issuer, "admin", InstitutionalRoles.GranSecretaria, "order")]
    [InlineData(Subject, Issuer, "admin", InstitutionalRoles.PlatformSuperAdmin, "lodge")]
    [InlineData(Subject, "https://evil.example.invalid/", "admin", InstitutionalRoles.PlatformSuperAdmin, "order")]
    public void Identity_or_role_variation_cannot_read_master_status(
        string subject, string issuer, string username, string role, string scope)
    {
        var principal = Principal(subject, issuer, username, role, scope);
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            principal, Access, Config()));
    }

    [Fact]
    public void Support_impersonation_or_unauthenticated_principal_is_denied()
    {
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(impersonation: true), Access, Config()));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(authenticated: false), Access, Config()));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            null, Access, Config()));
    }

    [Fact]
    public void Malconfigured_authority_or_duplicated_issuer_claim_is_denied()
    {
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(), Access, Config(authority: "https://another.example.invalid/")));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            Principal(), Access, Config(authority: null)));

        var identity = Principal();
        ((ClaimsIdentity)identity.Identity!).AddClaim(new Claim("iss", Issuer));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            identity, Access, Config()));
    }

    [Fact]
    public void No_request_header_username_or_email_can_replace_subject_claim()
    {
        var identity = Principal(subject: "other-subject");
        ((ClaimsIdentity)identity.Identity!).AddClaim(
            new Claim("X-User", "admin"));
        Assert.False(InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
            identity, Access, Config()));
    }
}
