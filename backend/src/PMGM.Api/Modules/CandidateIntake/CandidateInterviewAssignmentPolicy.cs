namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateInterviewAssignmentPolicy
{
    public const string Council = "administration_council";
    public const string Chamber = "masters_chamber";
    public const string Assigned = "assigned";
    public const string Completed = "completed";
    public const string Replaced = "replaced";

    public static bool ValidCouncil(string? value) => value is Council or Chamber;

    public static string? Validate(
        IReadOnlyCollection<Guid>? members, string? councilBody,
        DateOnly decisionDate, DateOnly today, string? minuteReference)
    {
        if (members is null || members.Count is < 3 or > 6)
            return "Se deben designar tres Maestros distintos; se admiten hasta tres adicionales justificados.";
        if (members.Any(id => id == Guid.Empty) || members.Distinct().Count() != members.Count)
            return "Cada entrevistador debe ser un Maestro diferente con identificador institucional válido.";
        if (!ValidCouncil(councilBody))
            return "Debe registrar si el acuerdo corresponde al Consejo de Administración o a la Cámara del Medio.";
        if (decisionDate > today || decisionDate < new DateOnly(2000, 1, 1))
            return "La fecha del acuerdo institucional no es válida.";
        if (string.IsNullOrWhiteSpace(minuteReference) || minuteReference.Trim().Length is < 5 or > 500)
            return "Debe ingresar referencia verificable del acta del acuerdo (5 a 500 caracteres).";
        return null;
    }
}
