namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Calcula únicamente el límite material de una reducción de ascenso.
/// Reglamento General art. 3.4: a lo sumo 50 % de los requisitos
/// de art. 3.2 d) y e). No acredita votación, acta, continuidad ni
/// resolución de Régimen Interior y NUNCA autoriza una ceremonia.
/// </summary>
public static class AdvancementDispensationReductionPolicy
{
    public const string SeniorityMonths = "seniority_months";

    public static bool TryCalculateEffectiveMinimum(
        string? requirementCode,
        int originalMinimum,
        int authorizedReduction,
        out int effectiveMinimum)
    {
        effectiveMinimum = originalMinimum;

        if (originalMinimum < 0 || authorizedReduction < 0)
            return false;

        // Antigüedad y asistencias son las únicas reducciones previstas
        // por art. 3.2 d/e y 3.4. Los dos trabajos de 3.2 b/c no son
        // dispensables. Una reducción cero conserva el mínimo original.
        var reducible = requirementCode is SeniorityMonths
            or AdvancementRequirementCodes.MeetingAttendance
            or AdvancementRequirementCodes.InstructionAttendance;

        if (!reducible)
            return requirementCode == AdvancementRequirementCodes.WorkPapers &&
                   authorizedReduction == 0;

        // División entera: ante mínimos impares no se permite reducir
        // más de la mitad redondeando hacia arriba.
        if (authorizedReduction > originalMinimum / 2)
            return false;

        effectiveMinimum = originalMinimum - authorizedReduction;
        return true;
    }
}
