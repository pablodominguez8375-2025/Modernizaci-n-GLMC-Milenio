using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Validates a candidate capture against a previously published, trusted schema.
/// The caller must resolve the schema and authorization from the server, not accept
/// a client-provided schema. This policy does not persist or authorize a capture.
/// </summary>
public static partial class FormSubmissionPolicy
{
    private const int MaxValues = 128;

    [GeneratedRegex(@"^\+?[0-9()\s.\-]{6,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    public static FormSchemaValidation Validate(
        FormSchemaSnapshot trustedSchema,
        int submittedVersion,
        IReadOnlyDictionary<string, JsonElement> submittedValues)
    {
        ArgumentNullException.ThrowIfNull(trustedSchema);
        ArgumentNullException.ThrowIfNull(submittedValues);

        var errors = new List<string>();
        if (trustedSchema.Version < 1 || submittedVersion != trustedSchema.Version)
            errors.Add("La versión del formulario no corresponde al esquema publicado.");

        if (trustedSchema.Fields is null || trustedSchema.Fields.Count is 0 or > MaxValues)
        {
            errors.Add("El esquema de captura no es válido.");
            return new(false, errors);
        }

        if (submittedValues.Count > MaxValues)
        {
            errors.Add("La captura supera el máximo de campos.");
            return new(false, errors);
        }

        var fields = trustedSchema.Fields.ToDictionary(x => x.Key, StringComparer.Ordinal);

        foreach (var (key, value) in submittedValues)
        {
            if (!fields.TryGetValue(key, out var field))
            {
                errors.Add("La captura contiene un campo no permitido por esta versión.");
                continue;
            }

            // Inactive, hidden and read-only fields may be populated server-side,
            // but may never be overwritten by an untrusted client capture.
            if (!field.Enabled || !field.Visible || !field.Editable)
            {
                errors.Add($"No se permite enviar el campo {key}.");
                continue;
            }

            if (field.DependsOnKey is { Length: > 0 } parentKey &&
                (!submittedValues.TryGetValue(parentKey, out var parentValue) ||
                 !IsDependencyActive(parentValue)))
            {
                errors.Add($"La dependencia de {key} no está satisfecha.");
                continue;
            }

            if (IsProvided(value) && !MatchesTypeAndOptions(field, value))
                errors.Add($"El valor de {key} no cumple el tipo o catálogo permitido.");
        }

        foreach (var field in trustedSchema.Fields)
        {
            if (!field.Required || !field.Enabled || !field.Visible || !field.Editable)
                continue; // Server-owned institutional fields are checked by domain APIs.

            if (field.DependsOnKey is { Length: > 0 } parentKey &&
                (!submittedValues.TryGetValue(parentKey, out var parent) || !IsDependencyActive(parent)))
                continue; // A conditional child is not required until its parent is present.

            if (!submittedValues.TryGetValue(field.Key, out var value) || !IsProvided(value))
                errors.Add($"Falta el campo obligatorio {field.Key}.");
        }

        return new(errors.Count == 0, errors);
    }

    private static bool IsProvided(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => false,
        JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
        JsonValueKind.Array => value.GetArrayLength() > 0,
        _ => true // 0 and false are legitimate submitted values.
    };

    // A boolean false does not activate a conditional child. A future
    // condition-expression policy will specify precise option-based predicates.
    private static bool IsDependencyActive(JsonElement value)
        => IsProvided(value) && value.ValueKind != JsonValueKind.False &&
           !(value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) && n == 0);

    private static bool MatchesTypeAndOptions(FormFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return true;

        if (field.TypeCode == "boolean")
            return value.ValueKind is JsonValueKind.True or JsonValueKind.False;

        if (field.TypeCode is "number" or "currency" or "percentage")
            return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _);

        if (field.TypeCode == "multi_select")
        {
            if (value.ValueKind != JsonValueKind.Array || field.Options is null)
                return false;

            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in value.EnumerateArray())
            {
                if (option.ValueKind != JsonValueKind.String ||
                    option.GetString() is not { } label ||
                    !field.Options.Contains(label, StringComparer.Ordinal) ||
                    !selected.Add(label))
                    return false;
            }
            return true;
        }

        if (value.ValueKind != JsonValueKind.String)
            return false;

        var text = value.GetString() ?? string.Empty;
        return field.TypeCode switch
        {
            "text" => text.Length <= 2000,
            "multiline" => text.Length <= 10000,
            "single_select" => field.Options?.Contains(text, StringComparer.Ordinal) == true,
            "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _),
            "time" => TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _),
            "email" => text.Length <= 254 && !text.Any(char.IsWhiteSpace) &&
                System.Net.Mail.MailAddress.TryCreate(text, out var mail) &&
                string.Equals(mail.Address, text, StringComparison.Ordinal),
            "phone" => PhonePattern().IsMatch(text) && text.Count(char.IsDigit) >= 6,
            // Client-side uploads must refer to an opaque, server-authorized ID;
            // never accept inline base64, URLs, paths or document content here.
            "document" or "photo" or "reference" => Guid.TryParse(text, out _),
            _ => false
        };
    }
}
