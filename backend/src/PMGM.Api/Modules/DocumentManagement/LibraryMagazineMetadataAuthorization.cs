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

    /// <summary>
    /// El listado editorial exige editar la categoría, no basta con
    /// acceso genérico a Biblioteca ni con leer una categoría distinta.
    /// </summary>
    /// <summary>
    /// Crear un borrador requiere una concesión de categoría "create" separada
    /// de "edit": nunca implica poder versionar, subir o publicar archivos.
    /// </summary>
    public static bool CanCreateEditorialMagazine(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog)
    {
        if (!access.HasOrderScope(user)) return false;
        var subject = DynamicTreasuryAccess.Subject(user);
        var today = DynamicAccessEndpoints.Today();
        return DynamicAccessEndpoints.IsCategoryGranted(
                   catalog, subject, "library", "create", "magazine", null, today) ||
               (access.CanManageLibrary(user, null) &&
                DynamicViewAccess.Allows(catalog, subject, "library", "create", null, today));
    }

    public static bool CanListEditableMagazines(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog)
    {
        if (!access.HasOrderScope(user)) return false;

        var subject = DynamicTreasuryAccess.Subject(user);
        var today = DynamicAccessEndpoints.Today();
        return DynamicAccessEndpoints.IsCategoryGranted(
                   catalog, subject, "library", "edit", "magazine", null, today) ||
               (access.CanManageLibrary(user, null) &&
                DynamicViewAccess.Allows(catalog, subject, "library", "edit", null, today));
    }

    public static bool CanEditMetadata(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog,
        InstitutionalDocument document)
    {
        if (!IsEligible(document) || !access.HasOrderScope(user)) return false;

        var subject = DynamicTreasuryAccess.Subject(user);
        var today = DynamicAccessEndpoints.Today();
        var categoryGrant = DynamicAccessEndpoints.IsCategoryGranted(
            catalog, subject, "library", "edit", "magazine", null, today);

        // El cargo Bibliotecario tampoco evita las restricciones técnicas
        // explícitas del catálogo, cuando existen asignaciones para el sujeto.
        return categoryGrant ||
            (access.CanManageLibrary(user, null) &&
             DynamicViewAccess.Allows(catalog, subject, "library", "edit", null, today));
    }
}
