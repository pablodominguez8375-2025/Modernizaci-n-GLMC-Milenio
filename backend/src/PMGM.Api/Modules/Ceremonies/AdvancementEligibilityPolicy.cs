namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Evalúa los requisitos propios del grado que se completa antes de un Aumento de Salario
/// o una Exaltación. Los valores mínimos se reciben desde configuración institucional;
/// esta política no contiene porcentajes ni cantidades fijas.
/// </summary>
public static class AdvancementEligibilityPolicy
{
    public static AdvancementEligibilityDecision Evaluate(
        string ceremonyType,
        AdvancementThresholds thresholds,
        AdvancementEvidence evidence,
        DispensationEvidence? dispensation = null)
    {
        var sourceDegree = ceremonyType switch
        {
            CeremonyCodes.Type.WageIncrease => 1,
            CeremonyCodes.Type.Exaltation => 2,
            _ => 0
        };

        if (sourceDegree == 0)
        {
            return new AdvancementEligibilityDecision(
                Applies: false,
                CanProceed: true,
                Mode: AdvancementEligibilityModes.NotApplicable,
                SourceDegree: 0,
                RuleVersion: thresholds.RuleVersion,
                Requirements: Array.Empty<AdvancementRequirementResult>(),
                Dispensation: null);
        }

        var requirements = new[]
        {
            EvaluateMinimum(
                AdvancementRequirementCodes.MeetingAttendance,
                "Asistencia a tenidas",
                thresholds.MinimumMeetingAttendance,
                evidence.MeetingAttendance),
            EvaluateMinimum(
                AdvancementRequirementCodes.InstructionAttendance,
                "Asistencia a instrucciones",
                thresholds.MinimumInstructionAttendance,
                evidence.InstructionAttendance),
            EvaluateMinimum(
                AdvancementRequirementCodes.WorkPapers,
                "Planchas de trabajo",
                thresholds.MinimumWorkPapers,
                evidence.WorkPapers)
        };

        var ordinaryComplies = requirements.All(x => x.Complies);
        if (ordinaryComplies)
        {
            return new AdvancementEligibilityDecision(
                Applies: true,
                CanProceed: true,
                Mode: AdvancementEligibilityModes.Ordinary,
                SourceDegree: sourceDegree,
                RuleVersion: thresholds.RuleVersion,
                Requirements: requirements,
                Dispensation: null);
        }

        var dispensationDecision = EvaluateDispensation(dispensation);
        var dispensationApproved = dispensationDecision?.Status == AdvancementDispensationStatuses.Approved;

        return new AdvancementEligibilityDecision(
            Applies: true,
            CanProceed: dispensationApproved,
            Mode: dispensationApproved
                ? AdvancementEligibilityModes.Dispensation
                : AdvancementEligibilityModes.Blocked,
            SourceDegree: sourceDegree,
            RuleVersion: thresholds.RuleVersion,
            Requirements: requirements,
            Dispensation: dispensationDecision);
    }

    private static AdvancementRequirementResult EvaluateMinimum(
        string code,
        string name,
        int minimum,
        int achieved)
    {
        if (minimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "El mínimo configurado no puede ser negativo.");
        }

        if (achieved < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(achieved), "El valor alcanzado no puede ser negativo.");
        }

        var complies = achieved >= minimum;
        return new AdvancementRequirementResult(
            Code: code,
            Name: name,
            Minimum: minimum,
            Achieved: achieved,
            Complies: complies,
            Status: complies
                ? CeremonyCodes.ValidationStatus.Approved
                : CeremonyCodes.ValidationStatus.Rejected);
    }

    private static AdvancementDispensationDecision? EvaluateDispensation(DispensationEvidence? evidence)
    {
        if (evidence is null)
        {
            return null;
        }

        if (!evidence.CouncilApproved)
        {
            return new AdvancementDispensationDecision(
                AdvancementDispensationStatuses.Rejected,
                "El Consejo de Maestros del Taller no aprobó la dispensa.",
                evidence.CouncilRecordReference,
                evidence.InternalAffairsResolutionReference);
        }

        if (string.Equals(
                evidence.InternalAffairsStatus,
                CeremonyCodes.ValidationStatus.ExceptionApproved,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                evidence.InternalAffairsStatus,
                CeremonyCodes.ValidationStatus.Approved,
                StringComparison.OrdinalIgnoreCase))
        {
            return new AdvancementDispensationDecision(
                AdvancementDispensationStatuses.Approved,
                "Dispensa acordada por el Consejo de Maestros y validada por Régimen Interior.",
                evidence.CouncilRecordReference,
                evidence.InternalAffairsResolutionReference);
        }

        if (string.Equals(
                evidence.InternalAffairsStatus,
                CeremonyCodes.ValidationStatus.Rejected,
                StringComparison.OrdinalIgnoreCase))
        {
            return new AdvancementDispensationDecision(
                AdvancementDispensationStatuses.Rejected,
                "Régimen Interior rechazó la dispensa.",
                evidence.CouncilRecordReference,
                evidence.InternalAffairsResolutionReference);
        }

        return new AdvancementDispensationDecision(
            AdvancementDispensationStatuses.PendingInternalAffairs,
            "La dispensa fue acordada por el Consejo de Maestros y está pendiente de resolución de Régimen Interior.",
            evidence.CouncilRecordReference,
            evidence.InternalAffairsResolutionReference);
    }
}

public static class AdvancementEligibilityModes
{
    public const string NotApplicable = "not_applicable";
    public const string Ordinary = "ordinary";
    public const string Dispensation = "dispensation";
    public const string Blocked = "blocked";
}

public static class AdvancementRequirementCodes
{
    public const string MeetingAttendance = "meeting_attendance";
    public const string InstructionAttendance = "instruction_attendance";
    public const string WorkPapers = "work_papers";
}

public static class AdvancementDispensationStatuses
{
    public const string PendingInternalAffairs = "pending_internal_affairs";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public sealed record AdvancementThresholds(
    int MinimumMeetingAttendance,
    int MinimumInstructionAttendance,
    int MinimumWorkPapers,
    string RuleVersion);

public sealed record AdvancementEvidence(
    int MeetingAttendance,
    int InstructionAttendance,
    int WorkPapers);

public sealed record DispensationEvidence(
    bool CouncilApproved,
    string? CouncilRecordReference,
    string InternalAffairsStatus,
    string? InternalAffairsResolutionReference);

public sealed record AdvancementRequirementResult(
    string Code,
    string Name,
    int Minimum,
    int Achieved,
    bool Complies,
    string Status);

public sealed record AdvancementDispensationDecision(
    string Status,
    string Reason,
    string? CouncilRecordReference,
    string? InternalAffairsResolutionReference);

public sealed record AdvancementEligibilityDecision(
    bool Applies,
    bool CanProceed,
    string Mode,
    int SourceDegree,
    string RuleVersion,
    IReadOnlyList<AdvancementRequirementResult> Requirements,
    AdvancementDispensationDecision? Dispensation);
