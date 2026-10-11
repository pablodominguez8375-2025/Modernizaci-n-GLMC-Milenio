namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>Reprogramación administrativa sin sustituir el acuerdo de designación.</summary>
public static class CandidateInterviewReschedulePolicy
{
    public static string? Validate(DateOnly scheduledDate, DateOnly today, string? reason)
    {
        if (scheduledDate < today || scheduledDate > today.AddYears(2))
            return "La nueva fecha debe ser actual o futura y estar dentro de los siguientes dos años.";
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 10 or > 1000)
            return "Registre el motivo de reprogramación (10 a 1000 caracteres).";
        return null;
    }
}
