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

    [Fact]
    public void Librarian_can_manage_only_library_and_requires_order_scope()
    {
        var librarian = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.Librarian));
        var withoutScope = Principal(
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.Librarian));

        Assert.True(access.CanManageLibrary(librarian, null));
        Assert.False(access.CanManageDocuments(librarian, null));
        Assert.False(access.CanManageGrandArchive(librarian));
        Assert.False(access.CanConfigureSystem(librarian));
        Assert.False(access.CanManageLibrary(withoutScope, null));
        Assert.False(access.CanManageLibrary(withoutScope, Guid.NewGuid()));
    }

    [Fact]
    public void Grand_archivist_cannot_inherit_library_or_document_repository_permissions()
    {
        var archivist = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GrandArchivist));

        Assert.True(access.CanManageGrandArchive(archivist));
        Assert.False(access.CanManageLibrary(archivist, null));
        Assert.False(access.CanManageDocuments(archivist, null));
    }

    [Fact]
    public void Legacy_document_manager_does_not_inherit_new_librarian_role()
    {
        var manager = Principal(
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.DocumentManager));
        Assert.True(access.CanManageDocuments(manager, null));
        Assert.False(access.CanManageLibrary(manager, null));
        Assert.False(access.CanManageGrandArchive(manager));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
