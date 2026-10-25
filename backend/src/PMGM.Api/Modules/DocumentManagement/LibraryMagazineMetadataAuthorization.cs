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

    /// <summary>Clave para auditoría: el tipo editorial canónico o el código de la categoría gestionada.</summary>
    public static string AuditKey(string? documentType) => Canonical(documentType) ?? documentType?.Trim().ToLowerInvariant() ?? "unknown";

    /// <summary>
    /// #588 — categorías que el Bibliotecario gestiona directamente (libros y las que cree). Quedan fuera las
    /// editoriales (Comunicaciones/Revista/Boletín), las planchas (flujo del Taller con revisión), los documentos
    /// oficiales (Gestión Documental) y videos.
    /// </summary>
    public static bool IsManagedType(string? documentType)
    {
        var t = documentType?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(t) || Canonical(t) is not null) return false;
        return t is not ("work_paper" or "working_paper" or "plancha" or "plancha_de_trabajo" or "official_document" or "video");
    }

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
public static class LibraryPublisherProfiles
{
    public const string Librarian = "librarian";
    public const string Communications = "communications";
}

public static class LibraryMagazineMetadataAuthorization
{
    // Perfiles SEPARADOS (decisión del PO 23-10): Bibliotecario y Comunicaciones
    // son cargos distintos. El administrador de Gran Logia y el Master de
    // plataforma NO ejercen ninguno de los dos: se comprueba el rol propio,
    // sin la herencia de GranLogiaAdmin ni de PlatformSuperAdmin.
    private static bool HasOwnRole(ClaimsPrincipal user, IInstitutionalAccessService access, string role)
        => access.HasOrderScope(user) && user.Claims.Any(x =>
            x.Type == InstitutionalClaims.Role &&
            string.Equals(x.Value, role, StringComparison.OrdinalIgnoreCase));

    public static bool IsLibrarianProfile(ClaimsPrincipal user, IInstitutionalAccessService access)
        => HasOwnRole(user, access, InstitutionalRoles.Librarian);

    private static bool IsCommunicationsProfile(ClaimsPrincipal user, IInstitutionalAccessService access)
        => HasOwnRole(user, access, InstitutionalRoles.Communications);

    /// <summary>
    /// Perfil con el que el usuario crea publicaciones. Quien reúne ambos
    /// cargos o ninguno (colaborador con grant) no fija perfil.
    /// </summary>
    public static string? ActorProfile(ClaimsPrincipal user, IInstitutionalAccessService access)
    {
        var lib = IsLibrarianProfile(user, access);
        var com = IsCommunicationsProfile(user, access);
        if (lib == com) return null;
        return lib ? LibraryPublisherProfiles.Librarian : LibraryPublisherProfiles.Communications;
    }

    /// <summary>
    /// «Propio» es por perfil/cargo, no por persona: quien ocupa el cargo con
    /// ese perfil hereda las publicaciones. Cada perfil no toca lo del otro y
    /// el administrador de Gran Logia no es titular de ninguno. Documentos sin
    /// perfil (históricos) no se restringen.
    /// </summary>
    public static bool OwnsPublication(
        ClaimsPrincipal user, IInstitutionalAccessService access, InstitutionalDocument document)
        => document.PublisherProfile switch
        {
            null => true,
            LibraryPublisherProfiles.Librarian => IsLibrarianProfile(user, access),
            LibraryPublisherProfiles.Communications => IsCommunicationsProfile(user, access),
            _ => false
        };

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

    public static bool IsManagedPublication(InstitutionalDocument document)
        => document.OrganizationId is null &&
           document.Collection is
           {
               OrganizationId: null,
               Scope: DocumentManagementCodes.Scope.Order,
               Status: DocumentManagementCodes.CollectionStatus.Active
           } &&
           document.Classification == DocumentManagementCodes.Classification.Internal &&
           document.AccessPolicy == DocumentManagementCodes.AccessPolicy.LibraryAuthenticated &&
           document.PublisherProfile == LibraryPublisherProfiles.Librarian &&
           LibraryEditorialTypes.IsManagedType(document.DocumentType);

    // Solo el perfil Bibliotecario (no colaboradores con grant, no Comunicaciones, no admin de Gran Logia).
    private static bool IsLibrarianFor(
        ClaimsPrincipal user, IInstitutionalAccessService access, DynamicAccessCatalog catalog, string action)
        => access.HasOrderScope(user) && IsLibrarianProfile(user, access) &&
           DynamicViewAccess.Allows(catalog, DynamicTreasuryAccess.Subject(user),
               "library", action, null, DynamicAccessEndpoints.Today());

    /// <summary>
    /// Bibliotecario o Comunicaciones institucional, respetando la restricción
    /// técnica dinámica de la vista Biblioteca cuando existe para el sujeto.
    /// </summary>
    private static bool IsInstitutionalEditor(
        ClaimsPrincipal user, IInstitutionalAccessService access,
        DynamicAccessCatalog catalog, string action)
        => (IsLibrarianProfile(user, access) || IsCommunicationsProfile(user, access)) &&
           DynamicViewAccess.Allows(catalog, DynamicTreasuryAccess.Subject(user),
               "library", action, null, DynamicAccessEndpoints.Today());

    private static bool CategoryGranted(
        ClaimsPrincipal user, DynamicAccessCatalog catalog, string action, string category)
        => DynamicAccessEndpoints.IsCategoryGranted(
            catalog, DynamicTreasuryAccess.Subject(user), "library", action,
            category, null, DynamicAccessEndpoints.Today());

    private static bool CanAct(
        ClaimsPrincipal user, IInstitutionalAccessService access,
        DynamicAccessCatalog catalog, string action, string category,
        InstitutionalDocument? document = null)
        => access.HasOrderScope(user) &&
           (CategoryGranted(user, catalog, action, category) ||
            (IsInstitutionalEditor(user, access, catalog, action) &&
             (document is null || OwnsPublication(user, access, document))));

    /// <summary>
    /// Publicar y retirar requieren competencia institucional (Bibliotecario
    /// o Comunicaciones); grants de colaborador no otorgan esta capacidad.
    /// </summary>
    public static bool CanPublishEditorialMagazine(
        ClaimsPrincipal user, IInstitutionalAccessService access,
        DynamicAccessCatalog catalog, InstitutionalDocument document)
        => document.Status != DocumentManagementCodes.DocumentStatus.Retired &&
           ((IsEligible(document) &&
             access.HasOrderScope(user) &&
             IsInstitutionalEditor(user, access, catalog, "edit") &&
             OwnsPublication(user, access, document)) ||
            (IsManagedPublication(document) && IsLibrarianFor(user, access, catalog, "edit")));

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
        => document.Status is DocumentManagementCodes.DocumentStatus.Draft
               or DocumentManagementCodes.DocumentStatus.Published &&
           ((IsEligible(document) && CanStageInState(user, access, catalog, document)) ||
            (IsManagedPublication(document) && IsLibrarianFor(user, access, catalog, "write")));

    // Borrador: grant "write" de colaborador o perfil titular. Publicado: sólo
    // el perfil titular puede preparar un reemplazo sin despublicar (el
    // colaborador sigue limitado a borradores).
    private static bool CanStageInState(
        ClaimsPrincipal user, IInstitutionalAccessService access,
        DynamicAccessCatalog catalog, InstitutionalDocument document)
    {
        var category = LibraryEditorialTypes.Canonical(document.DocumentType)!;
        if (document.Status == DocumentManagementCodes.DocumentStatus.Draft)
            return CanAct(user, access, catalog, "write", category, document);
        return access.HasOrderScope(user) &&
               IsInstitutionalEditor(user, access, catalog, "write") &&
               OwnsPublication(user, access, document);
    }

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
           CanAct(user, access, catalog, "edit", LibraryEditorialTypes.Canonical(document.DocumentType)!, document);
}
