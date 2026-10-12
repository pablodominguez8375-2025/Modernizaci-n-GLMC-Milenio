namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Institutional, server-controlled catalogue. An HTTP client cannot nominate
/// its own core fields or change the privacy floor of the candidate dossier.
/// This first API increment is intentionally limited to the candidate profile.
/// </summary>
public static class FormSchemaInstitutionalCatalog
{
    public const string CandidateProfile = "candidate_profile";

    private sealed record CoreRule(
        string TypeCode, string MinimumClassification, bool MustBeRequired,
        bool MustBeServerOwned = false);

    private static readonly IReadOnlyDictionary<string, CoreRule> CandidateCore =
        new Dictionary<string, CoreRule>(StringComparer.Ordinal)
        {
            ["candidate_id"] = new("reference", FormFieldClassification.Sensitive, true, true),
            ["organization_id"] = new("reference", FormFieldClassification.Internal, true, true),
            ["first_names"] = new("text", FormFieldClassification.Sensitive, true),
            ["paternal_surname"] = new("text", FormFieldClassification.Sensitive, true),
            ["maternal_surname"] = new("text", FormFieldClassification.Sensitive, false),
            ["rut_or_institutional_id"] = new("text", FormFieldClassification.Restricted, true),
            ["process_status"] = new("text", FormFieldClassification.Internal, true, true)
        };

    public static bool Supports(string? code)
        => string.Equals(code, CandidateProfile, StringComparison.Ordinal);

    public static IReadOnlyCollection<string> TrustedCoreKeys(string formCode)
        => Supports(formCode) ? CandidateCore.Keys.ToArray() :
            throw new ArgumentException("El formulario no está habilitado en el catálogo institucional.", nameof(formCode));

    public static FormSchemaValidation ValidateForPublication(FormSchemaSnapshot schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!Supports(schema.FormCode))
            return new(false, ["El formulario no está habilitado en el catálogo institucional."]);

        var errors = new List<string>();
        if (schema.Fields is null || schema.Fields.Any(field => field is null))
            return new(false, ["El formulario requiere una lista válida de campos sin elementos nulos."]);

        var indexed = schema.Fields
            .Where(field => field is not null && field.Key is not null)
            .GroupBy(field => field.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var (key, rule) in CandidateCore)
        {
            if (!indexed.TryGetValue(key, out var field))
            {
                errors.Add($"El campo núcleo {key} es obligatorio.");
                continue;
            }
            if (!field.IsCore || !field.Enabled || !field.Visible ||
                field.TypeCode != rule.TypeCode ||
                (rule.MustBeRequired && !field.Required) ||
                (rule.MustBeServerOwned && field.Editable))
                errors.Add($"El campo núcleo {key} no respeta el contrato institucional.");
            if (FormFieldClassification.Rank(field.Classification) <
                FormFieldClassification.Rank(rule.MinimumClassification))
                errors.Add($"No se puede reducir la privacidad del campo núcleo {key}.");
        }

        foreach (var field in schema.Fields)
        {
            if (field is null) continue;
            // A candidate's private dossier has no publicly configurable fields.
            var minimum = field.TypeCode switch
            {
                "document" or "photo" => FormFieldClassification.Restricted,
                "email" or "phone" => FormFieldClassification.Sensitive,
                _ => FormFieldClassification.Internal
            };
            if (FormFieldClassification.Rank(field.Classification) <
                FormFieldClassification.Rank(minimum))
                errors.Add($"La clasificación del campo {field.Key} no protege la información privada.");
        }

        return new(errors.Count == 0, errors);
    }
}
