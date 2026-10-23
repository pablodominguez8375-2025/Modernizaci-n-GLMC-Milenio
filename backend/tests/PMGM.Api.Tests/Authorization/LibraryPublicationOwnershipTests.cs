using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>
/// Regla 22-10-2026: cada perfil corrige solo sus propias publicaciones;
/// «propio» es por cargo/perfil, no por persona; se puede reemplazar el
/// archivo de una publicación vigente sin despublicarla.
/// </summary>
public sealed class LibraryPublicationOwnershipTests
{
    private readonly InstitutionalAccessService access = new();

    private static ClaimsPrincipal Identity(string role, string subject)
        => new(new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim(InstitutionalClaims.Role, role),
            new Claim(InstitutionalClaims.Scope, "order")
        ], "test"));

    private static InstitutionalDocument Document(string? profile, string status)
        => new()
        {
            DocumentType = "magazine",
            Status = status,
            PublisherProfile = profile,
            Classification = DocumentManagementCodes.Classification.Internal,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            Collection = new DocumentCollection
            {
                Scope = DocumentManagementCodes.Scope.Order,
                Status = DocumentManagementCodes.CollectionStatus.Active
            }
        };

    private const string Published = DocumentManagementCodes.DocumentStatus.Published;
    private const string Draft = DocumentManagementCodes.DocumentStatus.Draft;

    [Fact]
    public void Actor_profile_follows_the_role()
    {
        Assert.Equal(LibraryPublisherProfiles.Librarian,
            LibraryMagazineMetadataAuthorization.ActorProfile(Identity(InstitutionalRoles.Librarian, "a"), access));
        Assert.Equal(LibraryPublisherProfiles.Communications,
            LibraryMagazineMetadataAuthorization.ActorProfile(Identity(InstitutionalRoles.Communications, "b"), access));
    }

    [Fact]
    public void Another_user_with_the_same_profile_inherits_the_publication()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var doc = Document(LibraryPublisherProfiles.Communications, Published);
        var successor = Identity(InstitutionalRoles.Communications, "nuevo-titular");

        Assert.True(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(successor, access, catalog, doc));
        Assert.True(LibraryMagazineMetadataAuthorization.CanEditMetadata(successor, access, catalog, doc));
        Assert.True(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(successor, access, catalog, doc));
    }

    [Fact]
    public void Gran_logia_admin_and_platform_master_own_neither_profile()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        foreach (var role in new[] { InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.PlatformSuperAdmin })
        {
            var user = Identity(role, "admin-" + role);
            Assert.Null(LibraryMagazineMetadataAuthorization.ActorProfile(user, access));
            foreach (var profile in new[] { LibraryPublisherProfiles.Librarian, LibraryPublisherProfiles.Communications })
            {
                var doc = Document(profile, Published);
                Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(user, access, catalog, doc));
                Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(user, access, catalog, doc));
                Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(user, access, catalog, doc));
            }
        }
    }

    [Fact]
    public void Each_profile_cannot_touch_the_other_profiles_publications()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var librarian = Identity(InstitutionalRoles.Librarian, "bib");
        var communications = Identity(InstitutionalRoles.Communications, "com");
        var byComms = Document(LibraryPublisherProfiles.Communications, Published);
        var byLibrarian = Document(LibraryPublisherProfiles.Librarian, Published);

        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(librarian, access, catalog, byComms));
        Assert.False(LibraryMagazineMetadataAuthorization.CanEditMetadata(librarian, access, catalog, byComms));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(librarian, access, catalog, byComms));
        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(communications, access, catalog, byLibrarian));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(communications, access, catalog, byLibrarian));
        Assert.True(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(librarian, access, catalog, byLibrarian));
    }

    [Fact]
    public void Legacy_documents_without_profile_keep_the_previous_behavior()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var legacy = Document(null, Published);

        Assert.True(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            Identity(InstitutionalRoles.Librarian, "bib"), access, catalog, legacy));
        Assert.True(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            Identity(InstitutionalRoles.Communications, "com"), access, catalog, legacy));
    }

    [Fact]
    public void New_version_can_be_staged_on_a_published_document_but_not_on_a_retired_one()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var owner = Identity(InstitutionalRoles.Communications, "com");
        var profile = LibraryPublisherProfiles.Communications;

        Assert.True(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(owner, access, catalog, Document(profile, Draft)));
        Assert.True(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(owner, access, catalog, Document(profile, Published)));
        Assert.False(LibraryMagazineMetadataAuthorization.CanStageMagazineContent(
            owner, access, catalog, Document(profile, DocumentManagementCodes.DocumentStatus.Retired)));
    }

    [Fact]
    public void Roles_without_library_or_communications_competence_cannot_correct()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var doc = Document(LibraryPublisherProfiles.Librarian, Published);

        Assert.False(LibraryMagazineMetadataAuthorization.CanPublishEditorialMagazine(
            Identity(InstitutionalRoles.GrandArchivist, "archivero"), access, catalog, doc));
    }
}
