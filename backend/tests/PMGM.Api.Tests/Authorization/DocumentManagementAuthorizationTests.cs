using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class DocumentManagementAuthorizationTests
{
    private readonly InstitutionalAccessService access = new();

    [Fact]
    public void Document_manager_with_order_scope_can_manage_all_document_scopes()
    {
        var principal = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.DocumentManager));

        Assert.True(access.CanManageDocuments(principal, null));
        Assert.True(access.CanManageDocuments(principal, Guid.NewGuid()));
    }

    [Fact]
    public void Workshop_secretariat_can_manage_documents_only_for_its_workshop()
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.TallerSecretaria),
            new Claim(InstitutionalClaims.Organization, organizationId.ToString()));

        Assert.True(access.CanManageDocuments(principal, organizationId));
        Assert.False(access.CanManageDocuments(principal, null));
        Assert.False(access.CanManageDocuments(principal, Guid.NewGuid()));
    }

    [Fact]
    public void RegimenInterior_does_not_inherit_document_repository_management()
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior));

        Assert.False(access.CanManageDocuments(principal, null));
        Assert.False(access.CanManageDocuments(principal, organizationId));
        Assert.False(access.CanReadOrganizationLibrary(principal, organizationId));
    }

    [Fact]
    public void Organization_claim_allows_reading_only_that_workshop_library_projection()
    {
        var organizationId = Guid.NewGuid();
        var principal = Principal(new Claim(InstitutionalClaims.Organization, organizationId.ToString()));

        Assert.True(access.CanReadOrganizationLibrary(principal, organizationId));
        Assert.False(access.CanReadOrganizationLibrary(principal, Guid.NewGuid()));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
