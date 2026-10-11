using System.Text.RegularExpressions;

namespace PMGM.Api.Modules.FormConfiguration;

// Versioned contract only. Access control and persistence must be enforced by the
// future administrative API, not by client-provided metadata (Issue #37).
public sealed record FormSchemaSnapshot(
    string FormCode,
    int Version,
    DateTimeOffset EffectiveFromUtc,
    IReadOnlyList<FormFieldDefinition> Fields);

public sealed record FormFieldDefinition(
    string Key,
    string Label,
    string TypeCode,
    bool IsCore = false,
    bool Enabled = true,
    bool Visible = true,
    bool Required = false,
    bool Editable = true,
    string Classification = FormFieldClassification.Internal,
    int SortOrder = 0,
    string? HelpText = null,
    string? DependsOnKey = null,
    IReadOnlyList<string>? Options = null);

public static class FormFieldClassification
{
    public const string Public = "public";
    public const string Internal = "internal";
    public const string Sensitive = "sensitive";
    public const string Restricted = "restricted";

    public static int Rank(string? value) => value switch
    {
        Public => 0,
        Internal => 1,
        Sensitive => 2,
        Restricted => 3,
        _ => -1
    };
}

public sealed record FormSchemaValidation(bool IsValid, IReadOnlyList<string> Errors);

public static partial class FormSchemaPolicy
{
    private const int MaxFields = 128;
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "text", "multiline", "number", "date", "time", "currency", "percentage",
        "boolean", "single_select", "multi_select", "email", "phone", "document",
        "photo", "reference"
    };

    [GeneratedRegex("^[a-z][a-z0-9_]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    /// <summary>
    /// Validates publication of an immutable next-version form definition.
    /// trustedCoreKeys comes from the institutional domain, never from the request.
    /// Existing definitions must remain identifiable, even when disabled.
    /// </summary>
    public static FormSchemaValidation ValidateNext(
        FormSchemaSnapshot? current,
        FormSchemaSnapshot next,
        IReadOnlyCollection<string> trustedCoreKeys)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(trustedCoreKeys);

        var errors = new List<string>();
        var core = trustedCoreKeys.ToHashSet(StringComparer.Ordinal);
        if (!ValidCode(next.FormCode))
            errors.Add("El código del formulario debe ser estable y usar minúsculas/guiones bajos.");
        if (next.Version < 1 || (current is null && next.Version != 1))
            errors.Add("El primer esquema debe tener versión 1.");
        if (current is not null)
        {
            if (!string.Equals(current.FormCode, next.FormCode, StringComparison.Ordinal))
                errors.Add("No se puede cambiar el código de un formulario publicado.");
            if (next.Version != current.Version + 1)
                errors.Add("La versión nueva debe ser consecutiva.");
            if (next.EffectiveFromUtc <= current.EffectiveFromUtc)
                errors.Add("La fecha efectiva de la nueva versión debe avanzar.");
        }

        var fields = next.Fields;
        if (fields is null || fields.Count is 0 or > MaxFields)
        {
            errors.Add("El formulario debe tener entre 1 y 128 campos.");
            return new(false, errors);
        }

        var byKey = new Dictionary<string, FormFieldDefinition>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!ValidCode(field.Key))
            {
                errors.Add("Existe un identificador de campo inválido.");
                continue;
            }
            if (!byKey.TryAdd(field.Key, field))
                errors.Add($"El campo {field.Key} está duplicado.");
            if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > 120)
                errors.Add($"El campo {field.Key} requiere una etiqueta de 1 a 120 caracteres.");
            if (field.HelpText?.Length > 500)
                errors.Add($"La ayuda de {field.Key} supera 500 caracteres.");
            if (field.SortOrder is < 0 or > 10000)
                errors.Add($"El orden de {field.Key} está fuera del rango permitido.");
            if (!Types.Contains(field.TypeCode))
                errors.Add($"El tipo de {field.Key} no está permitido.");
            if (FormFieldClassification.Rank(field.Classification) < 0)
                errors.Add($"La clasificación de {field.Key} no está permitida.");
            if (field.Required && (!field.Enabled || !field.Visible))
                errors.Add($"El campo obligatorio {field.Key} debe permanecer activo y visible.");
            if (!field.Enabled && field.Editable)
                errors.Add($"Un campo deshabilitado no puede ser editable: {field.Key}.");
            if (field.IsCore != core.Contains(field.Key))
                errors.Add($"La condición de núcleo de {field.Key} no coincide con el catálogo institucional.");
            if (field.IsCore && (!field.Enabled || !field.Visible))
                errors.Add($"No se puede ocultar ni deshabilitar el campo núcleo {field.Key}.");

            var select = field.TypeCode is "single_select" or "multi_select";
            var options = field.Options;
            if (select && (options is null || options.Count == 0 ||
                    options.Count > 100 || options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 120) ||
                    options.Distinct(StringComparer.Ordinal).Count() != options.Count))
                errors.Add($"El campo {field.Key} debe tener opciones válidas y únicas.");
            if (!select && options is { Count: > 0 })
                errors.Add($"El campo no seleccionable {field.Key} no admite opciones.");
        }

        foreach (var coreKey in core)
            if (!byKey.ContainsKey(coreKey))
                errors.Add($"Falta el campo núcleo obligatorio {coreKey}.");

        foreach (var field in fields)
        {
            if (string.IsNullOrEmpty(field.DependsOnKey))
                continue;
            if (field.DependsOnKey == field.Key ||
                !byKey.TryGetValue(field.DependsOnKey, out var parent) ||
                !parent.Enabled || !field.Enabled)
                errors.Add($"Dependencia inválida o inactiva de {field.Key}.");
        }

        // A cycle may be present even if the keys exist; fail closed before publication.
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool HasCycle(string key)
        {
            if (visiting.Contains(key)) return true;
            if (!visited.Add(key)) return false;
            visiting.Add(key);
            if (byKey.TryGetValue(key, out var field) &&
                field.DependsOnKey is { Length: > 0 } dependency &&
                byKey.ContainsKey(dependency) && HasCycle(dependency))
                return true;
            visiting.Remove(key);
            return false;
        }
        if (byKey.Keys.Any(HasCycle))
            errors.Add("Las dependencias de campos no pueden formar ciclos.");

        if (current?.Fields is { } oldFields)
        {
            foreach (var old in oldFields)
            {
                if (!byKey.TryGetValue(old.Key, out var updated))
                {
                    errors.Add($"No se puede eliminar el campo histórico {old.Key}; deshabilítelo conservando su definición.");
                    continue;
                }
                if (!string.Equals(old.TypeCode, updated.TypeCode, StringComparison.Ordinal))
                    errors.Add($"No se puede cambiar el tipo de {old.Key} sin una migración aprobada.");
                if (FormFieldClassification.Rank(updated.Classification) <
                    FormFieldClassification.Rank(old.Classification))
                    errors.Add($"No se puede reducir la clasificación privada de {old.Key}.");
                if (old.IsCore && (!updated.IsCore || !updated.Enabled || !updated.Visible ||
                    (old.Required && !updated.Required)))
                    errors.Add($"No se puede debilitar el campo núcleo {old.Key}.");
            }
        }

        return new(errors.Count == 0, errors);
    }

    private static bool ValidCode(string? value)
        => value is not null && IdentifierPattern().IsMatch(value);
}
