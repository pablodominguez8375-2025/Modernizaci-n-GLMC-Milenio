using PMGM.Api.Modules.DocumentManagement;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Verifica únicamente si una versión de acta completa vinculada a una Tenida
/// está disponible para revisión documental. No comprueba firmas, acuerdo de
/// Cámara del Medio ni presentación/aprobación de la plancha.
/// </summary>
public static class AdvancementMeetingMinuteEvidencePolicy
{
    public static bool IsReviewableFullMinute(WorkPaperMeetingMinuteVersion? version, Guid organizationId)
        => version is not null &&
           version.Id != Guid.Empty &&
           organizationId != Guid.Empty &&
           version.OrganizationId == organizationId &&
           (version.DocumentStatus is DocumentManagementCodes.DocumentStatus.Draft or
               DocumentManagementCodes.DocumentStatus.Active or
               DocumentManagementCodes.DocumentStatus.Published) &&
           version.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available &&
           version.ContentType is ("application/pdf" or
               "application/vnd.openxmlformats-officedocument.wordprocessingml.document") &&
           DocumentIntegrity.IsValidSha256(version.Sha256) &&
           !string.IsNullOrWhiteSpace(version.ScanReference) &&
           !string.IsNullOrWhiteSpace(version.ObjectKey);
}

/// <summary>
/// Metadata respaldada por DocumentVersions unido al documento institucional.
/// No acredita contenido, firmas ni validación de autoridades.
/// </summary>
public sealed record WorkPaperMeetingMinuteVersion(
    Guid Id,
    Guid? OrganizationId,
    string DocumentStatus,
    string ContentType,
    string ProcessingStatus,
    string? Sha256,
    string? ScanReference,
    string? ObjectKey);
