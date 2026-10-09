namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Verificación estructural previa a la constatación institucional de los dos
/// trabajos del Reglamento General 3.2 b/c. Las referencias se consideran
/// pendientes de autenticación por la fuente maestra: este resultado jamás
/// sustituye el acta, su aprobación ni habilita la autorización de ascenso.
/// </summary>
public static class AdvancementWorkPaperEvidencePolicy
{
    public const string DegreeSymbolism = "degree_symbolism";
    public const string MasonicGeneralCulture = "masonic_general_culture";

    public static WorkPaperEvidenceReadiness Review(
        Guid memberId,
        Guid organizationId,
        int sourceDegree,
        DateOnly degreeStart,
        DateOnly asOf,
        IEnumerable<WorkPaperAttestationCandidate> candidates)
    {
        // No admitir evidencia sin identidad institucional y ventana cronológica.
        if (memberId == Guid.Empty || organizationId == Guid.Empty ||
            sourceDegree is not (1 or 2) || degreeStart >= asOf)
            return Missing();

        var eligible = candidates
            .Where(x => x.AuthorMemberId == memberId &&
                        x.OrganizationId == organizationId &&
                        x.AuthorDegree == sourceDegree &&
                        x.DocumentId != Guid.Empty &&
                        x.DocumentVersionId != Guid.Empty &&
                        x.HeldMeetingId != Guid.Empty &&
                        x.PresentationMinutesVersionId != Guid.Empty &&
                        x.CouncilApprovalMinutesVersionId != Guid.Empty &&
                        x.PresentationDate > degreeStart &&
                        x.PresentationDate <= asOf)
            .ToArray();

        // Si el mismo documento se presenta con clasificaciones contradictorias,
        // excluirlo íntegramente. No usar una versión para simbolismo y otra
        // para cultura general, ni permitir que un tercer archivo disimule eso.
        var ambiguousDocuments = eligible.GroupBy(x => x.DocumentId)
            .Where(group => group.Select(x => x.WorkKind).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var unambiguous = eligible.Where(x => !ambiguousDocuments.Contains(x.DocumentId))
            .ToArray();

        // Múltiples versiones de una misma plancha NO forman dos trabajos.
        var symbolicIds = unambiguous.Where(x => x.WorkKind == DegreeSymbolism)
            .Select(x => x.DocumentId).Distinct().ToArray();
        var cultureIds = unambiguous.Where(x => x.WorkKind == MasonicGeneralCulture)
            .Select(x => x.DocumentId).Distinct().ToArray();

        // Los dos tipos deben provenir de documentos diferentes. Una única
        // plancha etiquetada dos veces no satisface las letras b) y c).
        var twoDistinctDocuments = symbolicIds.Any(s => cultureIds.Any(c => c != s));
        return new WorkPaperEvidenceReadiness(
            symbolicIds.Length,
            cultureIds.Length,
            twoDistinctDocuments,
            false, // faltan autenticidad y acuerdo real de Cámara del Medio
            false,
            twoDistinctDocuments
                ? "Hay referencias estructurales para dos trabajos distintos; falta verificar actas, calificación, aprobación y entrega formal."
                : "Faltan referencias completas para dos trabajos diferentes: simbolismo del grado y cultura general masónica.");
    }

    private static WorkPaperEvidenceReadiness Missing() =>
        new(0, 0, false, false, false,
            "No hay contexto válido de Hermano, Taller, grado y período para revisar los dos trabajos.");
}

public sealed record WorkPaperAttestationCandidate(
    Guid DocumentId,
    Guid DocumentVersionId,
    Guid AuthorMemberId,
    Guid OrganizationId,
    int AuthorDegree,
    string WorkKind,
    DateOnly PresentationDate,
    Guid HeldMeetingId,
    Guid PresentationMinutesVersionId,
    Guid CouncilApprovalMinutesVersionId);

public sealed record WorkPaperEvidenceReadiness(
    int SymbolismCandidates,
    int GeneralCultureCandidates,
    bool HasTwoDifferentWorkTypesWithReferences,
    bool InstitutionallyAccredited,
    bool AuthorizesCeremony,
    string Reason);
