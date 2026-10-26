using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Dependency-free, paginated A4 PDF export for ONE already-authorized private
/// candidate-form revision. Emits only supplementary fields from the immutable
/// historical schema. No URLs, JavaScript, attachments, external fonts or PII
/// metadata. Characters outside WinAnsi are represented as [U+HEX] rather
/// than silently discarded or reinterpreted.
/// </summary>
public static class CandidateFormCapturePdf
{
    public const string MediaType = "application/pdf";
    public const int MaxPages = 120;
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double Left = 54;
    private const double Right = 54;
    private const double Bottom = 68;
    private const double Top = 72;
    private const double BodyWidth = PageWidth - Left - Right;

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
            throw new InvalidDataException("La captura no corresponde a la definición histórica institucional.");

        var extra = trustedHistoricalSchema.Fields
            .Where(field => !field.IsCore)
            .OrderBy(field => field.SortOrder)
            .ThenBy(field => field.Key, StringComparer.Ordinal)
            .ToArray();
        var keys = extra.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        if (values.Keys.Any(key => !keys.Contains(key)))
            throw new InvalidDataException("La captura contiene campos que no pertenecen al esquema histórico.");

        var layout = new Layout();
        layout.Add("Ficha de Insinuado", 17, true, "0.094 0.204 0.357", 22);
        layout.Add("CAMPOS ADICIONALES · COPIA PRIVADA", 10, true, "0.659 0.455 0.114", 17);
        layout.Add("Gran Logia Mixta de Chile · Proyecto Centenario", 10, true,
            "0.094 0.204 0.357", 17);
        layout.Add("Sólo datos complementarios autorizados. Esta copia no contiene " +
            "identidad, RUT ni información institucional canónica.", 9, false,
            "0.300 0.360 0.430", 22);
        layout.Add($"Versión del esquema: {capture.SchemaVersion}    Revisión: {capture.CaptureRevision}",
            10, true, "0.094 0.204 0.357", 12);
        layout.Add("Capturada (UTC): " + capture.CapturedAtUtc.ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            9, false, "0.320 0.360 0.420", 28);

        foreach (var field in extra)
        {
            if (!values.TryGetValue(field.Key, out var value)) continue;
            layout.Add(field.Label, 11, true, "0.094 0.204 0.357", 6);
            layout.Add(Value(value), 10, false, "0.117 0.170 0.244", 11);
            layout.Add($"Clave: {field.Key}   Tipo: {field.TypeCode}   " +
                $"Clasificación: {field.Classification}", 8, false,
                "0.320 0.360 0.420", 18);
        }

        layout.Add("RESERVADO: el archivo descargado debe permanecer bajo custodia institucional.",
            9, true, "0.520 0.340 0.100", 8);
        layout.Add("Para caracteres no representables en la tipografía PDF, se indica el código [U+HEX].",
            8, false, "0.320 0.360 0.420", 0);
        return BuildPdf(layout.Pages);
    }

    private static string Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => "No informado",
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "Sí",
        JsonValueKind.False => "No",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.Array when value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
            => string.Join(", ", value.EnumerateArray().Select(item => item.GetString())),
        _ => throw new InvalidDataException("El tipo del dato histórico no es exportable.")
    };

    private sealed class Layout
    {
        private readonly List<StringBuilder> _pages = [new StringBuilder()];
        private double _y = PageHeight - Top;
        public IReadOnlyList<StringBuilder> Pages => _pages;

        public void Add(string text, int fontSize, bool bold, string rgb, double spaceAfter)
        {
            foreach (var paragraph in Normalize(text).Split('\n'))
            {
                var lines = Wrap(paragraph, fontSize);
                foreach (var line in lines)
                {
                    EnsureSpace(fontSize + 5);
                    var x = Left.ToString("0.##", CultureInfo.InvariantCulture);
                    var y = _y.ToString("0.##", CultureInfo.InvariantCulture);
                    _pages[^1].Append(rgb).Append(" rg\nBT /")
                        .Append(bold ? "F2" : "F1").Append(' ')
                        .Append(fontSize.ToString(CultureInfo.InvariantCulture))
                        .Append(" Tf 1 0 0 1 ").Append(x).Append(' ').Append(y)
                        .Append(" Tm <").Append(HexWinAnsi(line))
                        .Append("> Tj ET\n");
                    _y -= fontSize + 5;
                }
            }
            _y -= spaceAfter;
            // Page breaks are demand-driven; never append a blank final page.
        }

        private void EnsureSpace(double height)
        {
            if (_y - height < Bottom) NewPage();
        }

        private void NewPage()
        {
            if (_pages.Count >= MaxPages)
                throw new InvalidDataException("El contenido supera el límite institucional de páginas PDF.");
            _pages.Add(new StringBuilder());
            _y = PageHeight - Top;
        }
    }

    private static List<string> Wrap(string input, int fontSize)
    {
        if (input.Length == 0) return [string.Empty];
        // Helvetica's widest standard glyph fits within one font-size unit.
        // Conservative wrapping prevents clipping for all-caps and narrow screens.
        var maxUnits = BodyWidth / fontSize / 1.02;
        var result = new List<string>();
        var current = new StringBuilder();
        var length = 0.0;
        foreach (var token in input.Split(' ', StringSplitOptions.None))
        {
            var normalizedToken = Printable(token);
            var remaining = normalizedToken;
            if (current.Length > 0 && length + 1 + remaining.Length > maxUnits)
            {
                result.Add(current.ToString());
                current.Clear();
                length = 0;
            }
            while (remaining.Length > maxUnits)
            {
                var chunkSize = Math.Max(1, (int)maxUnits);
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    length = 0;
                }
                result.Add(remaining[..chunkSize]);
                remaining = remaining[chunkSize..];
            }
            if (remaining.Length == 0) continue;
            if (current.Length > 0)
            {
                current.Append(' ');
                length++;
            }
            current.Append(remaining);
            length += remaining.Length;
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result.Count == 0 ? [string.Empty] : result;
    }

    private static string Normalize(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var rune in input.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Replace('\r', '\n').EnumerateRunes())
        {
            if (rune.Value is 10) builder.Append('\n');
            else if (rune.Value < 32 || rune.Value == 127) builder.Append(' ');
            else builder.Append(rune.ToString());
        }
        return builder.ToString();
    }

    internal static string Printable(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var rune in input.EnumerateRunes())
        {
            if (rune.Value is >= 32 and <= 126 or >= 160 and <= 255 ||
                WinAnsiSpecial.ContainsKey(rune.Value))
                builder.Append(rune.ToString());
            else
                builder.Append("[U+").Append(rune.Value.ToString("X", CultureInfo.InvariantCulture)).Append(']');
        }
        return builder.ToString();
    }

    private static readonly IReadOnlyDictionary<int, byte> WinAnsiSpecial =
        new Dictionary<int, byte>
        {
            [0x20AC] = 0x80, [0x201A] = 0x82, [0x0192] = 0x83, [0x201E] = 0x84,
            [0x2026] = 0x85, [0x2020] = 0x86, [0x2021] = 0x87, [0x02C6] = 0x88,
            [0x2030] = 0x89, [0x0160] = 0x8A, [0x2039] = 0x8B, [0x0152] = 0x8C,
            [0x017D] = 0x8E, [0x2018] = 0x91, [0x2019] = 0x92, [0x201C] = 0x93,
            [0x201D] = 0x94, [0x2022] = 0x95, [0x2013] = 0x96, [0x2014] = 0x97,
            [0x02DC] = 0x98, [0x2122] = 0x99, [0x0161] = 0x9A, [0x203A] = 0x9B,
            [0x0153] = 0x9C, [0x017E] = 0x9E, [0x0178] = 0x9F,
        };

    internal static string HexWinAnsi(string value)
    {
        var builder = new StringBuilder(value.Length * 2);
        foreach (var rune in value.EnumerateRunes())
        {
            byte octet;
            if (rune.Value is >= 32 and <= 126 or >= 160 and <= 255)
                octet = (byte)rune.Value;
            else if (!WinAnsiSpecial.TryGetValue(rune.Value, out octet))
                throw new InvalidDataException("La representación PDF no admite el carácter solicitado.");
            builder.Append(octet.ToString("X2", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    private static byte[] BuildPdf(IReadOnlyList<StringBuilder> pages)
    {
        // Objects: catalog 1, page tree 2, Helvetica 3, Helvetica-Bold 4,
        // then a page object and one content stream per page.
        var objects = new List<byte[]>
        {
            Array.Empty<byte>(),
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Array.Empty<byte>(),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>")
        };
        var pageRefs = new List<string>();
        for (var index = 0; index < pages.Count; index++)
        {
            var pageNo = objects.Count;
            var contentNo = pageNo + 1;
            pageRefs.Add($"{pageNo} 0 R");

            var body = pages[index].ToString();
            body += "0.40 0.43 0.49 rg\nBT /F1 8 Tf 1 0 0 1 54 40 Tm <" +
                HexWinAnsi($"Documento reservado - Página {index + 1} de {pages.Count}") + "> Tj ET\n";
            var stream = Encoding.ASCII.GetBytes(body);
            objects.Add(Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth.ToString("0.##", CultureInfo.InvariantCulture)} {PageHeight.ToString("0.##", CultureInfo.InvariantCulture)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentNo} 0 R >>"));
            using var block = new MemoryStream();
            WriteAscii(block, $"<< /Length {stream.Length} >>\nstream\n");
            block.Write(stream);
            WriteAscii(block, "\nendstream");
            objects.Add(block.ToArray());
        }
        objects[2] = Encoding.ASCII.GetBytes(
            $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", pageRefs)}] >>");

        using var file = new MemoryStream();
        WriteAscii(file, "%PDF-1.4\n");
        file.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);
        var offsets = new long[objects.Count];
        for (var id = 1; id < objects.Count; id++)
        {
            offsets[id] = file.Position;
            WriteAscii(file, $"{id} 0 obj\n");
            file.Write(objects[id]);
            WriteAscii(file, "\nendobj\n");
        }
        var xref = file.Position;
        WriteAscii(file, $"xref\n0 {objects.Count}\n0000000000 65535 f \n");
        for (var id = 1; id < objects.Count; id++)
            WriteAscii(file, $"{offsets[id]:D10} 00000 n \n");
        WriteAscii(file, $"trailer\n<< /Size {objects.Count} /Root 1 0 R >>\n" +
            $"startxref\n{xref}\n%%EOF\n");
        return file.ToArray();
    }

    private static void WriteAscii(Stream stream, string text)
        => stream.Write(Encoding.ASCII.GetBytes(text));
}
