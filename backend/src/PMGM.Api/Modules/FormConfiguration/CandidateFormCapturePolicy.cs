using System.Text;
using System.Text.Json;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Fail-closed adapter for extra fields only. The actual core candidate dossier
/// remains the source of truth for identity, membership and workflow fields.
/// </summary>
public static class CandidateFormCapturePolicy
{
    public const int MaxPayloadBytes = 32 * 1024;

    public static FormSchemaValidation Validate(
        FormSchemaSnapshot trustedSchema,
        int requestedSchemaVersion,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(trustedSchema);
        ArgumentNullException.ThrowIfNull(values);

        var errors = new List<string>();
        if (!FormSchemaInstitutionalCatalog.Supports(trustedSchema.FormCode) ||
            !FormSchemaInstitutionalCatalog.ValidateForPublication(trustedSchema).IsValid)
            errors.Add("La definición institucional del formulario no es válida.");

        if (trustedSchema.Fields is null || trustedSchema.Fields.Count == 0)
            return new(false, ["La definición del formulario no contiene campos."]);

        var configured = trustedSchema.Fields.Where(x => !x.IsCore).ToArray();
        if (configured.Length == 0)
            errors.Add("No hay campos configurables publicados para capturar.");

        // Never copy names, RUT, person identifiers or workflow state from an
        // untrusted JSON field; their values remain exclusively domain-owned.
        if (values.Keys.Any(key => trustedSchema.Fields.Any(x => x.IsCore && x.Key == key)))
            errors.Add("Los campos institucionales no pueden sobrescribirse desde la captura configurable.");

        // Opaque IDs for photos/files/references need a separate resource
        // authorization step, which is not in this increment.
        if (configured.Any(field =>
            values.ContainsKey(field.Key) &&
            field.TypeCode is "document" or "photo" or "reference"))
            errors.Add("Los adjuntos y referencias requieren vinculación institucional verificada.");

        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(values)) > MaxPayloadBytes)
            errors.Add("Los campos configurables superan el máximo de 32 KiB.");

        if (configured.Length != 0)
        {
            var customSchema = trustedSchema with { Fields = configured };
            var typed = FormSubmissionPolicy.Validate(customSchema, requestedSchemaVersion, values);
            errors.AddRange(typed.Errors);
        }
        else if (requestedSchemaVersion != trustedSchema.Version || values.Count > 0)
        {
            errors.Add("Versión o valores de captura no permitidos.");
        }

        return new(errors.Count == 0, errors);
    }
}
