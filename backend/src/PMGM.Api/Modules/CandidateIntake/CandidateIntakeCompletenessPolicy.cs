using PMGM.Api.Modules.CandidateIntake.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

public sealed record CandidateIntakeCompletenessResult(int Completed, int Total, int Percent, IReadOnlyList<string> MissingRequirements)
{
    public bool IsComplete => Completed == Total;
}

public static class CandidateIntakeCompletenessPolicy
{
    public static CandidateIntakeCompletenessResult Evaluate(
        CandidateIntakeProfile profile,
        string? firstNames,
        string? phone,
        string? email,
        string? address)
    {
        var checks = new (bool Complete, string Missing)[]
        {
            (!string.IsNullOrWhiteSpace(firstNames), "Nombres"),
            (!string.IsNullOrWhiteSpace(profile.PaternalSurname), "Apellido paterno"),
            (!string.IsNullOrWhiteSpace(profile.RutOrInstitutionalId), "RUT o identificación"),
            (profile.BirthDate is not null, "Fecha de nacimiento"),
            (!string.IsNullOrWhiteSpace(profile.Nationality), "Nacionalidad"),
            (!string.IsNullOrWhiteSpace(profile.CivilStatus), "Estado civil"),
            (!string.IsNullOrWhiteSpace(phone), "Teléfono personal"),
            (!string.IsNullOrWhiteSpace(email), "Correo electrónico"),
            (!string.IsNullOrWhiteSpace(address), "Dirección personal"),
            (!string.IsNullOrWhiteSpace(profile.City), "Ciudad"),
            (!string.IsNullOrWhiteSpace(profile.Occupation), "Actividad, profesión u oficio"),
            (!string.IsNullOrWhiteSpace(profile.EmployerName), "Empleador"),
            (!string.IsNullOrWhiteSpace(profile.WorkAddress), "Dirección laboral"),
            (!string.IsNullOrWhiteSpace(profile.WorkPosition), "Cargo o función"),
            (!string.IsNullOrWhiteSpace(profile.WorkPhone), "Teléfono laboral"),
            (!string.IsNullOrWhiteSpace(profile.Orient), "Oriente"),
            (HasPresenters(profile.PresentersJson), "Presentantes"),
            (profile.FirstDegreePresentationDate is not null, "Fecha de presentación en primer grado"),
            (!string.IsNullOrWhiteSpace(profile.ResponsibleSecretaryName), "Secretario responsable"),
            (profile.PhotoVersionId is not null, "Fotografía tipo pasaporte")
        };

        var missing = checks.Where(x => !x.Complete).Select(x => x.Missing).ToArray();
        var completed = checks.Length - missing.Length;
        return new(completed, checks.Length, completed * 100 / checks.Length, missing);
    }

    private static bool HasPresenters(string json)
    {
        try { return (System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? []).Any(x => !string.IsNullOrWhiteSpace(x)); }
        catch (System.Text.Json.JsonException) { return false; }
    }
}
