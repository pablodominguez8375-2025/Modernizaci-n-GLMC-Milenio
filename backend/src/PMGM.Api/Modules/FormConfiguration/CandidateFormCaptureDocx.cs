using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Generates one private, read-only-by-convention DOCX from the original immutable
/// candidate capture. No external links, macros, pictures, or canonical identity.
/// Not a replacement for access control or legal records retention.
/// </summary>
public static class CandidateFormCaptureDocx
{
    public const string MediaType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string WordNs =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string PackageRelationshipsNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ContentTypesNs =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] Render(
        CandidateFormCapture capture,
        FormSchemaSnapshot trustedHistoricalSchema,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(trustedHistoricalSchema);
        ArgumentNullException.ThrowIfNull(values);

        if (capture.CaptureRevision < 1 ||
            trustedHistoricalSchema.FormCode != FormSchemaInstitutionalCatalog.CandidateProfile ||
            trustedHistoricalSchema.Version != capture.SchemaVersion ||
            !FormSchemaInstitutionalCatalog.ValidateForPublication(trustedHistoricalSchema).IsValid)
            throw new InvalidDataException("La captura no corresponde al esquema histórico institucional.");

        var configured = trustedHistoricalSchema.Fields
            .Where(field => !field.IsCore)
            .OrderBy(field => field.SortOrder)
            .ThenBy(field => field.Key, StringComparer.Ordinal)
            .ToArray();
        var keys = configured.Select(field => field.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (values.Keys.Any(key => !keys.Contains(key)))
            throw new InvalidDataException("La captura contiene campos sin definición histórica exportable.");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddXml(zip, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", ContentTypesNs);
                writer.WriteStartElement("Default", ContentTypesNs);
                writer.WriteAttributeString("Extension", "rels");
                writer.WriteAttributeString("ContentType",
                    "application/vnd.openxmlformats-package.relationships+xml");
                writer.WriteEndElement();
                writer.WriteStartElement("Default", ContentTypesNs);
                writer.WriteAttributeString("Extension", "xml");
                writer.WriteAttributeString("ContentType", "application/xml");
                writer.WriteEndElement();
                writer.WriteStartElement("Override", ContentTypesNs);
                writer.WriteAttributeString("PartName", "/word/document.xml");
                writer.WriteAttributeString("ContentType",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
            AddXml(zip, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", PackageRelationshipsNs);
                writer.WriteStartElement("Relationship", PackageRelationshipsNs);
                writer.WriteAttributeString("Id", "rId1");
                writer.WriteAttributeString("Type",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
                writer.WriteAttributeString("Target", "word/document.xml");
                writer.WriteEndElement();
                writer.WriteEndElement();
            });
            AddXml(zip, "word/document.xml", writer =>
            {
                writer.WriteStartElement("w", "document", WordNs);
                writer.WriteStartElement("w", "body", WordNs);

                WriteParagraph(writer, "GRAN LOGIA MIXTA DE CHILE", true, "17365A", 27, 130);
                WriteParagraph(writer, "PROYECTO CENTENARIO · DOCUMENTO PRIVADO",
                    true, "B38B30", 18, 260);
                WriteParagraph(writer, "Ficha de Insinuado — campos adicionales históricos",
                    true, "17365A", 32, 180);
                WriteParagraph(writer, "Exportación individual de una revisión. No contiene identidad, " +
                    "RUT ni datos canónicos de la ficha institucional.", false, "4E5969", 19, 300);
                WriteParagraph(writer, $"Versión del formulario: {capture.SchemaVersion}  ·  " +
                    $"Revisión: {capture.CaptureRevision}", true, "17365A", 21, 80);
                WriteParagraph(writer, "Fecha de captura UTC: " +
                    capture.CapturedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss",
                        System.Globalization.CultureInfo.InvariantCulture),
                    false, "4E5969", 19, 260);

                foreach (var field in configured)
                {
                    if (!values.TryGetValue(field.Key, out var value)) continue;
                    WriteParagraph(writer, field.Label, true, "17365A", 24, 70);
                    WriteParagraph(writer, FormatValue(value), false, "1D2736", 21, 80);
                    WriteParagraph(writer,
                        $"Clave: {field.Key}  ·  Tipo: {field.TypeCode}  ·  Clasificación: {field.Classification}",
                        false, "596678", 16, 240);
                }

                WriteParagraph(writer, "Documento reservado: su acceso, copia y custodia " +
                    "se rigen por los permisos y procedimientos institucionales.", false,
                    "7B5B19", 18, 140);
                writer.WriteStartElement("w", "sectPr", WordNs);
                writer.WriteStartElement("w", "pgSz", WordNs);
                writer.WriteAttributeString("w", "w", WordNs, "11906");
                writer.WriteAttributeString("w", "h", WordNs, "16838");
                writer.WriteEndElement();
                writer.WriteStartElement("w", "pgMar", WordNs);
                foreach (var (key, value) in new[]
                {
                    ("top", "1180"), ("right", "1300"), ("bottom", "1180"), ("left", "1300")
                })
                    writer.WriteAttributeString("w", key, WordNs, value);
                writer.WriteEndElement();
                writer.WriteEndElement(); // sectPr
                writer.WriteEndElement(); // body
                writer.WriteEndElement(); // document
            });
        }
        return buffer.ToArray();
    }

    private static void AddXml(ZipArchive zip, string path, Action<XmlWriter> write)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false,
        });
        writer.WriteStartDocument();
        write(writer);
        writer.WriteEndDocument();
    }

    private static void WriteParagraph(
        XmlWriter writer, string text, bool bold, string color, int size, int after)
    {
        writer.WriteStartElement("w", "p", WordNs);
        writer.WriteStartElement("w", "pPr", WordNs);
        writer.WriteStartElement("w", "spacing", WordNs);
        writer.WriteAttributeString("w", "after", WordNs,
            after.ToString(System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteEndElement();

        var parts = SanitizeXmlText(text).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteStartElement("w", "r", WordNs);
                writer.WriteStartElement("w", "br", WordNs);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteStartElement("w", "r", WordNs);
            writer.WriteStartElement("w", "rPr", WordNs);
            if (bold)
            {
                writer.WriteStartElement("w", "b", WordNs);
                writer.WriteEndElement();
            }
            writer.WriteStartElement("w", "rFonts", WordNs);
            writer.WriteAttributeString("w", "ascii", WordNs, "Aptos");
            writer.WriteAttributeString("w", "hAnsi", WordNs, "Aptos");
            writer.WriteEndElement();
            writer.WriteStartElement("w", "color", WordNs);
            writer.WriteAttributeString("w", "val", WordNs, color);
            writer.WriteEndElement();
            writer.WriteStartElement("w", "sz", WordNs);
            writer.WriteAttributeString("w", "val", WordNs,
                size.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteStartElement("w", "t", WordNs);
            writer.WriteAttributeString("xml", "space",
                "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteString(parts[i]);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static string SanitizeXmlText(string text)
    {
        var safe = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;
            if (value is 9 or 10 or 13 ||
                value >= 32 && value <= 0xD7FF ||
                value >= 0xE000 && value <= 0xFFFD ||
                value >= 0x10000 && value <= 0x10FFFF)
                safe.Append(rune.ToString());
        }
        return safe.ToString();
    }

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => "No informado",
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "Sí",
        JsonValueKind.False => "No",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.Array when value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String)
            => string.Join(", ", value.EnumerateArray().Select(x => x.GetString())),
        _ => throw new InvalidDataException("La captura contiene un tipo no exportable.")
    };
}
