namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Matriz de consulta para Secretaría/Gran Secretaría. No constituye resolución
/// institucional, no acredita trabajo presentado y jamás autoriza ceremonias.
/// </summary>
public static class AdvancementReviewMatrixPolicy
{
    public static AdvancementReviewMatrix Build(
        string ceremonyType,
        AdvancementAttendanceResult attendance,
        AdvancementRuleSnapshot? countRule,
        AdvancementSeniorityRuleReview seniority)
    {
        var expectedGrade = AdvancementAttendanceProjection.SourceGrade(ceremonyType);
        var data = attendance.Status == "ready" &&
                   attendance.Snapshot?.CeremonyType == ceremonyType &&
                   attendance.Snapshot.SourceGrade == expectedGrade
            ? attendance.Snapshot : null;

        var rows = new[]
        {
            CountRow("meeting_attendance", "Tenidas realizadas con presencia",
                countRule?.Configuration.MinimumMeetingAttendance, data?.Meetings.Present,
                countRule?.Thresholds.RuleVersion),
            CountRow("instruction_attendance", "Instrucciones realizadas con presencia",
                countRule?.Configuration.MinimumInstructionAttendance, data?.Instructions.Present,
                countRule?.Thresholds.RuleVersion),
            // No existe una constancia institucional verificada de presentación
            // y aprobación de los dos trabajos. Ni publicación ni vínculo con
            // Tenida celebrada acreditan un número de trabajos cumplidos.
            new AdvancementReviewRow(
                "work_papers", "Planchas presentadas y aprobadas",
                countRule?.Configuration.MinimumWorkPapers, null,
                countRule is null ? "rule_missing" : "presentation_unverified",
                false, countRule?.Thresholds.RuleVersion,
                "Los archivos y enlaces a Tenidas son sólo candidatos; faltan presentación, acta y aprobación institucional."),
            new AdvancementReviewRow(
                "complete_months", "Antigüedad continuada en el grado",
                seniority.MinimumCompleteMonths > 0 ? seniority.MinimumCompleteMonths : null,
                seniority.CompleteCalendarMonths,
                seniority.Status,
                false, seniority.RuleVersion,
                seniority.Reason)
        };
        return new AdvancementReviewMatrix(
            ceremonyType,
            data?.GradeStartDate,
            data?.AsOf,
            countRule?.RuleId,
            rows,
            // Esta revisión no sustituye la constatación de Cámara del Medio,
            // la continuidad firmada ni el expediente de dispensa.
            "pending_institutional_validation",
            false,
            false,
            "Pendiente de constancias de presentación/aprobación de trabajos, continuidad y resolución de elegibilidad; no habilita la autorización.");
    }

    private static AdvancementReviewRow CountRow(
        string code, string label, int? minimum, int? observed, string? version)
    {
        var status = minimum is null
            ? "rule_missing"
            : observed is null
                ? "evidence_missing"
                : observed < minimum
                    ? "minimum_not_reached"
                    : "threshold_reached_pending_verification";

        return new AdvancementReviewRow(code, label, minimum, observed, status,
            false, version, status switch
            {
                "rule_missing" => "No consta mínimo institucional versionado vigente.",
                "evidence_missing" => "No se pudo reconstruir actividad del grado y período.",
                "minimum_not_reached" => "El conteo observado no alcanza el mínimo vigente.",
                _ => "El conteo observado alcanza el mínimo, pero requiere validación institucional."
            });
    }
}

public sealed record AdvancementReviewRow(
    string Code,
    string Label,
    int? Required,
    int? Observed,
    string Status,
    bool InstitutionallyCertified,
    string? RuleVersion,
    string Reason);

public sealed record AdvancementReviewMatrix(
    string CeremonyType,
    DateOnly? GradeStartDate,
    DateOnly? AsOf,
    Guid? CountRuleId,
    IReadOnlyList<AdvancementReviewRow> Requirements,
    string Status,
    bool AllRequirementsCertified,
    bool AuthorizesCeremony,
    string Reason);
