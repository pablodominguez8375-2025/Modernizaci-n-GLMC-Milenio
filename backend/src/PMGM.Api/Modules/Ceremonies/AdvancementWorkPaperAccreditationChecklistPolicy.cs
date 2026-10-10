namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Lista de evidencias para revisión HUMANA de una plancha. Un paquete documental
/// completo no equivale a presentación, acuerdo de Cámara del Medio ni ascenso.
/// Consume exclusivamente los vínculos ya filtrados por Taller, grado, Tenida
/// celebrada, autor y versión en AdvancementWorkPaperEndpoints.
/// </summary>
public static class AdvancementWorkPaperAccreditationChecklistPolicy
{
    public static WorkPaperAccreditationChecklist Build(WorkPaperReviewCandidate candidate)
    {
        var held = (candidate.LinkedHeldMeetingIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty).ToHashSet();
        var extracts = (candidate.SubmittedExtractMeetingIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty).ToHashSet();
        var minutes = (candidate.ReviewableFullMinuteMeetingIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty).ToHashSet();

        // No componer un "paquete" con extracto de una Tenida y acta de otra:
        // todas las referencias deben corresponder a la misma Tenida celebrada.
        var coherentMeetings = held.Intersect(extracts).Intersect(minutes)
            .Order().ToArray();
        var hasDocument = candidate.ContentVerified && candidate.ReviewVersionId is not null;
        var pending = new List<string>();
        if (!hasDocument)
            pending.Add("usable_work_paper_version");
        if (held.Count == 0)
            pending.Add("held_meeting_link");
        if (!held.Overlaps(extracts))
            pending.Add("submitted_extract_same_meeting");
        if (!held.Overlaps(minutes))
            pending.Add("reviewable_full_minutes_same_meeting");
        if (held.Overlaps(extracts) && held.Overlaps(minutes) &&
            coherentMeetings.Length == 0)
            pending.Add("extract_and_full_minutes_same_meeting");

        // Ninguna de estas fuentes documentales acredita el contenido
        // de la deliberación, firmas, votos o aprobación institucional.
        pending.Add("human_verification_of_presentation");
        pending.Add("human_verification_of_chamber_approval");

        return new WorkPaperAccreditationChecklist(
            candidate.DocumentId,
            candidate.ReviewVersionId,
            hasDocument && coherentMeetings.Length > 0,
            coherentMeetings,
            pending,
            PresentationCertified: false,
            ApprovalCertified: false);
    }
}

public sealed record WorkPaperAccreditationChecklist(
    Guid DocumentId,
    Guid? ReviewVersionId,
    bool DocumentaryPacketReadyForReview,
    IReadOnlyList<Guid> SameMeetingPacketIds,
    IReadOnlyList<string> PendingEvidenceCodes,
    bool PresentationCertified,
    bool ApprovalCertified);
