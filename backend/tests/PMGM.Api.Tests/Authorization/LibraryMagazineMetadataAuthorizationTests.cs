using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class LibraryMagazineMetadataAuthorizationTests
{
    private static InstitutionalDocument Document(
        string type = "revista",
        string classification = DocumentManagementCodes.Classification.Internal,
        string policy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
        bool workshop = false,
        bool activeCollection = true)
    {
        var organizationId = workshop ? Guid.NewGuid() : (Guid?)null;
        return new InstitutionalDocument
        {
            DocumentType = type,
            Classification = classification,
            AccessPolicy = policy,
            OrganizationId = organizationId,
            Collection = new DocumentCollection
            {
                Scope = workshop
                    ? DocumentManagementCodes.Scope.Organization
                    : DocumentManagementCodes.Scope.Order,
                OrganizationId = organizationId,
                Status = activeCollection
                    ? DocumentManagementCodes.CollectionStatus.Active
                    : DocumentManagementCodes.CollectionStatus.Retired
            }
        };
    }

    private static ClaimsPrincipal Identity(string role, string subject = "colaborador", bool order = true)
        => new(new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim(InstitutionalClaims.Role, role),
            new Claim(InstitutionalClaims.Scope, order ? "order" : "lodge")
        ], "test"));

    private static DynamicAccessCatalog MagazineGrant(
        string subject = "colaborador",
        bool active = true,
        int daysOffset = 0,
        string category = "magazine",
        string area = "library")
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        catalog.Profiles.Add(new DynamicProfile(
            Guid.NewGuid(), "colaborador-revista", "Colaborador Revista",
            "order", false, true, ["documents"],
            [new DynamicGrant(area, ["view", "edit"], [category])]));
        var today = DynamicAccessEndpoints.Today();
        catalog.Assignments.Add(new DynamicAssignment(
            Guid.NewGuid(), subject, "colaborador-revista", null,
            today.AddDays(daysOffset - 1),
            today.AddDays(daysOffset + 1), active));
        return catalog;
    }

    [Theory]
    [InlineData("magazine")]
    [InlineData("magazines")]
    [InlineData("revista")]
    [InlineData("revistas")]
    public void Grant_only_edits_internal_order_magazine(string type)
    {
        var grants = MagazineGrant();
        var access = new InstitutionalAccessService();
        var user = Identity("collaborator");
        var doc = Document(type);
        Assert.True(LibraryMagazineMetadataAuthorization.CanEditMetadata(user, access, grants, doc));
        Assert.False(DynamicAccessEndpoints.IsGranted(
            grants, "colaborador", "documentmanager", "edit", null, DynamicAccessEndpoints.Today()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, grants, Document("book")));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, grants, Document(type, workshop: true)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, grants, Document(type, activeCollection: false)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, grants, Document(type, classification: DocumentManagementCodes.Classification.Confidential)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, grants, Document(type, policy: DocumentManagementCodes.AccessPolicy.ManagementOnly)));
    }

    [Fact]
    public void Revoked_expired_wrong_subject_category_or_area_denies_access()
    {
        var access = new InstitutionalAccessService();
        var user = Identity("collaborator");
        var doc = Document();
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            Identity("collaborator", "otro"), access, MagazineGrant(), doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, MagazineGrant(active: false), doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, MagazineGrant(daysOffset: -10), doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, MagazineGrant(category: "book"), doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            user, access, MagazineGrant(area: "documentmanager"), doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            Identity("collaborator", order: false), access, MagazineGrant(), doc));
    }

    [Fact]
    public void Librarian_may_edit_magazine_without_delegation_but_archivist_may_not()
    {
        var access = new InstitutionalAccessService();
        var grants = DynamicAccessEndpoints.CreateCatalog();
        var doc = Document();
        Assert.True(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            Identity(InstitutionalRoles.Librarian), access, grants, doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            Identity(InstitutionalRoles.GrandArchivist), access, grants, doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            Identity(InstitutionalRoles.DocumentManager), access, grants, doc));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            Identity(InstitutionalRoles.Librarian), access, grants, Document("book")));

        // Un bibliotecario sometido a un perfil limitado sólo a lectura
        // no obtiene permiso de editar a través de la excepción de metadatos.
        grants.Profiles.Add(new DynamicProfile(
            Guid.NewGuid(), "bibliotecario-lectura", "Biblioteca lectura",
            "order", false, true, ["documents"],
            [new DynamicGrant("library", ["view"], ["magazine"])]));
        grants.Assignments.Add(new DynamicAssignment(
            Guid.NewGuid(), "colaborador", "bibliotecario-lectura", null,
            DynamicAccessEndpoints.Today().AddDays(-1), null, true));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            Identity(InstitutionalRoles.Librarian), access, grants, doc));
    }
    [Fact]
    public void Write_grant_stages_draft_magazine_but_cannot_stage_other_documents()
    {
        var access = new InstitutionalAccessService();
        var me = Identity("collaborator");
        var grant = MagazineGrant();
        grant.Profiles[^1] = grant.Profiles[^1] with
        {
            Grants = [new DynamicGrant("library", ["view", "write"], ["magazine"])]
        };
        Assert.True(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, grant, Document()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            me, access, grant)); // write no implica create
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            me, access, grant, Document())); // ni editar
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            Identity("collaborator", "otro"), access, grant, Document()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, MagazineGrant(), Document())); // solo edit
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, grant, Document("book")));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, grant, Document(workshop: true)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, grant, Document(activeCollection: false)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, grant, Document(classification: DocumentManagementCodes.Classification.Confidential)));
        var published = Document();
        published.Status = DocumentManagementCodes.DocumentStatus.Published;
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, grant, published));
        var doc = Document();
        Assert.True(LibraryMagazineMetadataAuthorization.CanStageOwnMagazineVersion(
            me, access, grant, new DocumentVersion { Document = doc, CreatedBySubject = "colaborador" }));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageOwnMagazineVersion(
            me, access, grant, new DocumentVersion { Document = doc, CreatedBySubject = "otro" }));
        grant.Assignments[0] = grant.Assignments[0] with { IsActive = false };
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            me, access, grant, Document()));
    }

    [Fact]
    public void Create_draft_is_separate_from_edit_and_requires_live_category_grant()
    {
        var access = new InstitutionalAccessService();
        var person = Identity("collaborator");
        var createGrant = MagazineGrant();
        createGrant.Profiles[^1] = createGrant.Profiles[^1] with
        {
            Grants = [new DynamicGrant("library", ["view", "create"], ["magazine"])]
        };
        Assert.True(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            person, access, createGrant));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(
            person, access, createGrant, Document()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            person, access, MagazineGrant())); // solo view+edit
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            Identity("collaborator", "otro"), access, createGrant));
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            Identity("collaborator", order: false), access, createGrant));

        createGrant.Assignments[0] = createGrant.Assignments[0] with { IsActive = false };
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            person, access, createGrant));
        Assert.True(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            Identity(InstitutionalRoles.Librarian), access,
            DynamicAccessEndpoints.CreateCatalog()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanCreateEditorialMagazine(
            Identity(InstitutionalRoles.GrandArchivist), access,
            DynamicAccessEndpoints.CreateCatalog()));
    }

    [Fact]
    public void Editorial_list_requires_live_magazine_edit_grant_not_just_general_read()
    {
        var access = new InstitutionalAccessService();
        var principal = Identity("collaborator");
        Assert.True(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            principal, access, MagazineGrant()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            principal, access, MagazineGrant(active: false)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            principal, access, MagazineGrant(daysOffset: -10)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            Identity("collaborator", "distinto"), access, MagazineGrant()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            principal, access, MagazineGrant(category: "book")));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            principal, access, MagazineGrant(area: "documentmanager")));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            Identity("collaborator", order: false), access, MagazineGrant()));

        var viewOnly = MagazineGrant();
        viewOnly.Profiles[viewOnly.Profiles.Count - 1] = viewOnly.Profiles[^1] with
        {
            Grants = [new DynamicGrant("library", ["view"], ["magazine"])]
        };
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            principal, access, viewOnly));

        Assert.True(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            Identity(InstitutionalRoles.Librarian), access,
            DynamicAccessEndpoints.CreateCatalog()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            Identity(InstitutionalRoles.GrandArchivist), access,
            DynamicAccessEndpoints.CreateCatalog()));
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            Identity(InstitutionalRoles.DocumentManager), access,
            DynamicAccessEndpoints.CreateCatalog()));

        // Incluso con cargo de Bibliotecario, un perfil técnico de solo lectura
        // o de una categoría ajena no puede abrir el listado editable.
        Assert.False(LibraryMagazineMetadataAuthorization.CanListEditableMagazines(
            Identity(InstitutionalRoles.Librarian), access, MagazineGrant(category: "book")));
    }

}
