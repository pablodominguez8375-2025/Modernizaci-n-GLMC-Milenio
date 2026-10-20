using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

/// <summary>
/// Delegación excepcional para editar metadatos de revistas institucionales.
/// No autoriza contenidos, versiones, publicación, documentos de Taller
/// ni otras categorías o áreas del catálogo.
/// </summary>
public static class LibraryMagazineMetadataAuthorization
{
    public static bool IsEligible(InstitutionalDocument document)
        => document.OrganizationId is null &&
           document.Collection is
           {
               OrganizationId: null,
               Scope: DocumentManagementCodes.Scope.Order,
               Status: DocumentManagementCodes.CollectionStatus.Active
           } &&
           document.Classification == DocumentManagementCodes.Classification.Internal &&
           document.AccessPolicy == DocumentManagementCodes.AccessPolicy.LibraryAuthenticated &&
           document.DocumentType.Trim().ToLowerInvariant() is
               "magazine" or "magazines" or "revista" or "revistas";

    public static bool CanEditMetadata(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog,
        InstitutionalDocument document)
    {
        if (!IsEligible(document) || !access.HasOrderScope(user)) return false;
        if (access.CanManageLibrary(user, null)) return true;

        var subject = DynamicTreasuryAccess.Subject(user);
        return DynamicAccessEndpoints.IsCategoryGranted(
            catalog, subject, "library", "edit", "magazine",
            null, DynamicAccessEndpoints.Today());
    }
}
