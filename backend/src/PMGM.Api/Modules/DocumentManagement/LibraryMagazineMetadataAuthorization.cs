using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

/// <summary>
/// Categorías editoriales de Biblioteca Virtual: la revista institucional
/// (Horizonte Masónico) y el Boletín mensual de actividades de la Orden.
/// </summary>
public static class LibraryEditorialTypes
{
    public const string Magazine = "magazine";
    public const string Bulletin = "bulletin";

    /// <summary>Tipo canónico o null cuando no es una publicación editorial.</summary>
    public static string? Canonical(string? documentType)
        => documentType?.Trim().ToLowerInvariant() switch
        {
            "magazine" or "magazines" or "revista" or "revistas" => Magazine,
            "bulletin" or "bulletins" or "boletin" or "boletines" or "boletín" => Bulletin,
            _ => null
        };

    public static bool IsMagazine(string? documentType) => Canonical(documentType) == Magazine;
    public static bool IsBulletin(string? documentType) => Canonical(documentType) == Bulletin;
}

/// <summary>
/// Autorización editorial de Revista y Boletín. Combina tres vías, todas
/// sujetas a las restricciones dinámicas explícitas del catálogo:
/// el cargo Bibliotecario, el perfil institucional Comunicaciones y los
/// colaboradores con grant por categoría (sólo su categoría exacta).
/// No autoriza documentos de Taller ni otras categorías o áreas.
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
           LibraryEditorialTypes.Canonical(document.DocumentType) is not null;

    /// <summary>
    /// Bibliotecario o Comunicaciones institucional, respetando la restricción
    /// técnica dinámica de la vista Biblioteca cuando existe para el sujeto.
    /// </summary>
    private static bool IsInstitutionalEditor(
        ClaimsPrincipal user, IInstitutionalAccessService access,
        DynamicAccessCatalog catalog, string action)
        => (access.CanManageLibrary(user, null) || access.CanManageCommunications(user)) &&
           DynamicViewAccess.Allows(catalog, DynamicTreasuryAccess.Subject(user),
               "library", action, null, DynamicAccessEndpoints.Today());

    private static bool CategoryGranted(
        ClaimsPrincipal user, DynamicAccessCatalog catalog, string action, string category)
        => DynamicAccessEndpoints.IsCategoryGranted(
            catalog, DynamicTreasuryAccess.Subject(user), "library", action,
            category, null, DynamicAccessEndpoints.Today());

    private static bool CanAct(
        ClaimsPrincipal user, IInstitutionalAccessService access,
        DynamicAccessCatalog catalog, string action, string category)
        => access.HasOrderScope(user) &&
           (CategoryGranted(user, catalog, action, category) ||
            IsInstitutionalEditor(user, access, catalog, action));

    /// <summary>
    /// Publicar y retirar requieren competencia institucional (Bibliotecario
    /// o Comunicaciones); grants de colaborador no otorgan esta capacidad.
    /// </summary>
    public static bool CanPublishEditorialMagazine(
        ClaimsPrincipal user, IInstitutionalAccessService access,
        DynamicAccessCatalog catalog, InstitutionalDocument document)
        => IsEligible(document) &&
           document.Status != DocumentManagementCodes.DocumentStatus.Retired &&
           access.HasOrderScope(user) &&
           IsInstitutionalEditor(user, access, catalog, "edit");

    /// <summary>Alta de borrador de cualquier categoría editorial (filtro de ruta).</summary>
    public static bool CanCreateEditorialMagazine(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog)
        => CanCreateEditorial(user, access, catalog, LibraryEditorialTypes.Magazine) ||
           CanCreateEditorial(user, access, catalog, LibraryEditorialTypes.Bulletin);

    /// <summary>
    /// Crear un borrador requiere una concesión "create" de esa categoría,
    /// separada de "edit": nunca implica poder versionar, subir o publicar.
    /// </summary>
    public static bool CanCreateEditorial(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog,
        string category)
        => LibraryEditorialTypes.Canonical(category) is { } canonical &&
           CanAct(user, access, catalog, "create", canonical);

    /// <summary>El listado editorial exige editar al menos una categoría editorial.</summary>
    public static bool CanListEditableMagazines(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog)
        => CanListEditable(user, access, catalog, LibraryEditorialTypes.Magazine) ||
           CanListEditable(user, access, catalog, LibraryEditorialTypes.Bulletin);

    public static bool CanListEditable(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog,
        string category)
        => CanAct(user, access, catalog, "edit", category);

    /// <summary>
    /// Reserva/carga/análisis de una versión editorial en borrador.
    /// El grant "write" es independiente de crear/editar metadatos o publicar.
    /// </summary>
    public static bool CanStageMagazineContent(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog,
        InstitutionalDocument document)
        => IsEligible(document) &&
           document.Status == DocumentManagementCodes.DocumentStatus.Draft &&
           CanAct(user, access, catalog, "write", LibraryEditorialTypes.Canonical(document.DocumentType)!);

    public static bool CanStageOwnMagazineVersion(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog,
        DocumentVersion version)
        => string.Equals(version.CreatedBySubject,
               DynamicTreasuryAccess.Subject(user), StringComparison.Ordinal) &&
           CanStageMagazineContent(user, access, catalog, version.Document);

    public static bool CanEditMetadata(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        DynamicAccessCatalog catalog,
        InstitutionalDocument document)
        => IsEligible(document) &&
           CanAct(user, access, catalog, "edit", LibraryEditorialTypes.Canonical(document.DocumentType)!);
}
