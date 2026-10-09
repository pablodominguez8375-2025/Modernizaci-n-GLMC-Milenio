using PMGM.Api.Modules.DocumentManagement;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Comprobación de vigencia e integridad del extracto del Taller al construir
/// evidencia privada de revisión. No acredita presentación de la plancha,
/// aprobación de Cámara del Medio ni autoriza ceremonia alguna.
/// </summary>
public static class AdvancementExtractEvidencePolicy
{
    public static bool IsReviewableExtract(WorkPaperExtractVersion? version, Guid organizationId)
        => version is not null &&
           version.Id != Guid.Empty &&
           organizationId != Guid.Empty &&
           version.OrganizationId == organizationId &&
           (version.DocumentStatus is DocumentManagementCodes.DocumentStatus.Draft or
               DocumentManagementCodes.DocumentStatus.Active or
               DocumentManagementCodes.DocumentStatus.Published) &&
           version.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available &&
           version.ContentType == "application/pdf" &&
           DocumentIntegrity.IsValidSha256(version.Sha256) &&
           !string.IsNullOrWhiteSpace(version.ScanReference) &&
           !string.IsNullOrWhiteSpace(version.ObjectKey);
}

/// <summary>
/// Proyección de datos de la fuente documental real, NO referencia declarada
/// por Secretaría. La pertenencia debe obtenerse de InstitutionalDocument.
/// </summary>
public sealed record WorkPaperExtractVersion(
    Guid Id,
    Guid? OrganizationId,
    string DocumentStatus,
    string ContentType,
    string ProcessingStatus,
    string? Sha256,
    string? ScanReference,
    string? ObjectKey);
