using System.Text;
using System.Text.Json;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Restricted CSV representation of one already-authorized historical capture.
/// Uses only the schema version originally associated with the submission.
/// Never includes canonical candidate identity or document links.
/// </summary>
public static class CandidateFormCaptureCsv
{
    public static byte[] Render(
        CandidateFormCapture capture,
        FormSchemaSnapshot trustedHistoricalSchema,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(trustedHistoricalSchema);
        ArgumentNullException.ThrowIfNull(values);

        if (trustedHistoricalSchema.FormCode != FormSchemaInstitutionalCatalog.CandidateProfile ||
            trustedHistoricalSchema.Version != capture.SchemaVersion ||
            !FormSchemaInstitutionalCatalog.ValidateForPublication(trustedHistoricalSchema).IsValid)
            throw new InvalidDataException("La versión histórica no corresponde a la captura institucional.");

        var indexed = trustedHistoricalSchema.Fields
            .Where(field => !field.IsCore)
            .ToDictionary(field => field.Key, StringComparer.Ordinal);
        if (values.Keys.Any(key => !indexed.ContainsKey(key)))
            throw new InvalidDataException("La captura contiene claves sin definición histórica admisible.");

        var builder = new StringBuilder();
        builder.AppendLine("Campo;Clave;Valor;Clasificación;Tipo;Versión;Revisión;Fecha captura (UTC)");
        foreach (var field in indexed.Values.OrderBy(field => field.SortOrder).ThenBy(field => field.Key, StringComparer.Ordinal))
        {
            if (!values.TryGetValue(field.Key, out var value)) continue;
            var cells = new[]
            {
                field.Label, field.Key, FormatValue(value), field.Classification,
                field.TypeCode, capture.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                capture.CaptureRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                capture.CapturedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
            };
            builder.AppendLine(string.Join(";", cells.Select(EscapeCell)));
        }
        // Excel's UTF-8 auto-detection requires BOM on many Spanish-language installations.
        return Encoding.UTF8.GetBytes("\uFEFF" + builder.ToString());
    }

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => string.Empty,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "Sí",
        JsonValueKind.False => "No",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.Array when value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
            => string.Join(" | ", value.EnumerateArray().Select(item => item.GetString())),
        _ => throw new InvalidDataException("La captura histórica contiene un tipo de valor no exportable.")
    };

    private static string EscapeCell(string? value)
    {
        var text = value ?? string.Empty;
        // Spreadsheet formula injection: Excel and LibreOffice may execute a
        // leading =, +, -, or @ even after whitespace/control characters.
        // Prefix untrusted text before quoting, rather than altering stored data.
        var firstVisible = 0;
        while (firstVisible < text.Length &&
               (char.IsWhiteSpace(text[firstVisible]) ||
                char.IsControl(text[firstVisible]) ||
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text[firstVisible]) ==
                    System.Globalization.UnicodeCategory.Format))
            firstVisible++;
        if (firstVisible < text.Length && "=+-@".Contains(text[firstVisible]))
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
