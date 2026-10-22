using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>
/// Perfiles institucionales Bibliotecario y Comunicaciones (#635):
/// Comunicaciones publica la revista y el Boletín mensual, y nada más.
/// </summary>
public sealed class CommunicationsEditorialAuthorizationTests
{
    private readonly InstitutionalAccessService access = new();

    private static ClaimsPrincipal Identity(string role, string subject = "comunicaciones", bool order = true)
        => new(new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim(InstitutionalClaims.Role, role),
            new Claim(InstitutionalClaims.Scope, order ? "order" : "lodge")
        ], "test"));

    private static InstitutionalDocument Document(
        string type,
        string status = DocumentManagementCodes.DocumentStatus.Draft,
        bool workshop = false)
    {
        var organizationId = workshop ? Guid.NewGuid() : (Guid?)null;
        return new InstitutionalDocument
        {
            DocumentType = type,
            Status = status,
            Classification = DocumentManagementCodes.Classification.Internal,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            OrganizationId = organizationId,
            Collection = new DocumentCollection
            {
                Scope = workshop ? DocumentManagementCodes.Scope.Organization : DocumentManagementCodes.Scope.Order,
                OrganizationId = organizationId,
                Status = DocumentManagementCodes.CollectionStatus.Active
            }
        };
    }

    [Fact]
    public void Communications_profile_is_order_scoped_and_does_not_inherit_other_areas()
    {
        var communications = Identity(InstitutionalRoles.Communications);
        var withoutScope = Identity(InstitutionalRoles.Communications, order: false);

        Assert.True(access.CanManageCommunications(communications));
        Assert.False(access.CanManageCommunications(withoutScope));
        Assert.False(access.CanManageLibrary(communications, null));
        Assert.False(access.CanManageDocuments(communications, null));
        Assert.False(access.CanManageGrandArchive(communications));
        Assert.False(access.CanConfigureSystem(communications));
        Assert.False(access.CanManageGrandSecretariat(communications));

        // El Bibliotecario no es Comunicaciones ni viceversa.
        Assert.False(access.CanManageCommunications(Identity(InstitutionalRoles.Librarian)));
        Assert.False(access.CanManageCommunications(Identity(InstitutionalRoles.GrandArchivist)));
        Assert.False(access.CanManageCommunications(Identity(InstitutionalRoles.DocumentManager)));
    }

    [Theory]
    [InlineData("magazine")]
    [InlineData("revista")]
    [InlineData("bulletin")]
    [InlineData("boletin")]
    [InlineData("boletines")]
    public void Communications_creates_stages_edits_and_publishes_magazines_and_bulletins(string type)
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var user = Identity(InstitutionalRoles.Communications);
        var canonical = LibraryEditorialTypes.Canonical(type)!;

        Assert.True(LibraryMagazineMetadataAuthorization.CanCreateEditorial(user, access, catalog, canonical));
        Assert.True(LibraryMagazineMetadataAuthorization.CanListEditable(user, access, catalog, canonical));
        Assert.True(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(user, access, catalog, Document(type)));
        Assert.True(LibraryMagazineMetadataAuthorization.CanEditMetadata(user, access, catalog, Document(type)));
        Assert.True(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(user, access, catalog, Document(type)));
        Assert.True(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            user, access, catalog, Document(type, DocumentManagementCodes.DocumentStatus.Published)));
    }

    [Theory]
    [InlineData("book")]
    [InlineData("work_paper")]
    [InlineData("official_document")]
    [InlineData("video")]
    public void Communications_cannot_touch_other_library_categories(string type)
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var user = Identity(InstitutionalRoles.Communications);

        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorial(user, access, catalog, type));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(user, access, catalog, Document(type)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(user, access, catalog, Document(type)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(user, access, catalog, Document(type)));
    }

    [Fact]
    public void Communications_cannot_publish_workshop_documents_or_retired_bulletins()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var user = Identity(InstitutionalRoles.Communications);

        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            user, access, catalog, Document("bulletin", workshop: true)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            user, access, catalog, Document("bulletin", DocumentManagementCodes.DocumentStatus.Retired)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            Identity(InstitutionalRoles.Communications, order: false), access, catalog, Document("bulletin")));
    }

    [Fact]
    public void Librarian_also_manages_bulletins_but_archivist_does_not()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var bulletin = Document("bulletin");

        Assert.True(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            Identity(InstitutionalRoles.Librarian), access, catalog, bulletin));
        Assert.True(LibraryMagazineMetadataAuthorization.CanCreateEditorial(
            Identity(InstitutionalRoles.Librarian), access, catalog, "bulletin"));
        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            Identity(InstitutionalRoles.GrandArchivist), access, catalog, bulletin));
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorial(
            Identity(InstitutionalRoles.DocumentManager), access, catalog, "bulletin"));
    }

    [Fact]
    public void Communications_respects_explicit_dynamic_restriction_to_read_only()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        catalog.Profiles.Add(new DynamicProfile(
            Guid.NewGuid(), "comunicaciones-lectura", "Comunicaciones lectura",
            "order", false, true, ["documents"],
            [new DynamicGrant("library", ["view"], [])]));
        catalog.Assignments.Add(new DynamicAssignment(
            Guid.NewGuid(), "comunicaciones", "comunicaciones-lectura", null,
            DynamicAccessEndpoints.Today().AddDays(-1), null, true));
        var user = Identity(InstitutionalRoles.Communications);

        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorial(user, access, catalog, "bulletin"));
        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(user, access, catalog, Document("bulletin")));
    }

    [Fact]
    public void Magazine_collaborator_grant_does_not_extend_to_bulletins()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        catalog.Profiles.Add(new DynamicProfile(
            Guid.NewGuid(), "colaborador-revista", "Colaborador Revista",
            "order", false, true, ["documents"],
            [new DynamicGrant("library", ["view", "create", "edit", "write"], ["magazine"])]));
        catalog.Assignments.Add(new DynamicAssignment(
            Guid.NewGuid(), "colaborador", "colaborador-revista", null,
            DynamicAccessEndpoints.Today().AddDays(-1), null, true));
        var user = Identity("collaborator", "colaborador");

        Assert.True(LibraryMagazineMetadataAuthorization.CanCreateEditorial(user, access, catalog, "magazine"));
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorial(user, access, catalog, "bulletin"));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(user, access, catalog, Document("bulletin")));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(user, access, catalog, Document("bulletin")));
        // Un colaborador nunca publica: sigue siendo competencia institucional.
        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(user, access, catalog, Document("magazine")));
    }

    [Theory]
    [InlineData("bulletin")]
    [InlineData("bulletins")]
    [InlineData("boletin")]
    [InlineData("Boletines")]
    [InlineData("boletín")]
    public void Bulletin_aliases_resolve_to_its_own_catalog_kind(string type)
    {
        Assert.Equal(LibraryEditorialTypes.Bulletin, LibraryEditorialTypes.Canonical(type));
        Assert.Contains("bulletin", DynamicAccessEndpoints.CatalogCategories);
    }

    [Fact]
    public void Session_exposes_library_and_communications_capabilities_separately()
    {
        var communications = SessionProfileBuilder.Build(Identity(InstitutionalRoles.Communications), access);
        var librarian = SessionProfileBuilder.Build(Identity(InstitutionalRoles.Librarian), access);

        Assert.True(communications.Capabilities.CanManageCommunications);
        Assert.False(communications.Capabilities.CanManageLibrary);
        Assert.False(communications.Capabilities.CanManageDocuments);
        Assert.True(librarian.Capabilities.CanManageLibrary);
        Assert.False(librarian.Capabilities.CanManageCommunications);
        Assert.False(librarian.Capabilities.CanManageDocuments);
    }
}
