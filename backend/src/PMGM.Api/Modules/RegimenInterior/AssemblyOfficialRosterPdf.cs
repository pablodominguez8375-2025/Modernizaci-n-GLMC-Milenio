using System.Globalization;
using System.Text;
using PMGM.Api.Modules.FormConfiguration;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// PDF A4 horizontal, sin dependencias, de un listado OFICIAL de una Asamblea ya cerrada
/// (asambleístas o sufragantes). Tabla paginada con encabezado repetido, columna «Firma» en blanco
/// y pie con el resumen SHA-256 del cierre. Solo contiene lo que recibe del llamador (RUT ya enmascarado).
/// </summary>
public static class AssemblyOfficialRosterPdf
{
    public const int MaxPages = 400;
    private const double W = 841.89, H = 595.28, Left = 40, Right = 40, Top = 48, Bottom = 46;
    private const int Font = 9;

    public static byte[] Render(string title, IReadOnlyList<string> infoLines, IReadOnlyList<string> columns,
        IReadOnlyList<double> widths, IReadOnlyList<IReadOnlyList<string>> rows, string footerNote)
    {
        if (columns.Count != widths.Count || columns.Count == 0) throw new ArgumentException("Columnas inválidas.");
        var pages = new List<StringBuilder>();
        StringBuilder page = null!; double y = 0;
        var firstPage = true;

        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        void Text(double x, double yy, string s, bool bold, int size, string rgb) =>
            page.Append(rgb).Append(" rg\nBT /").Append(bold ? "F2" : "F1").Append(' ').Append(size)
                .Append(" Tf 1 0 0 1 ").Append(F(x)).Append(' ').Append(F(yy)).Append(" Tm <")
                .Append(CandidateFormCapturePdf.HexWinAnsi(CandidateFormCapturePdf.Printable(s))).Append("> Tj ET\n");

        void NewPage()
        {
            if (pages.Count >= MaxPages) throw new InvalidDataException("El listado supera el límite de páginas del PDF.");
            page = new StringBuilder(); pages.Add(page); y = H - Top;
            if (firstPage)
            {
                Text(Left, y, title, true, 16, "0.094 0.204 0.357"); y -= 20;
                foreach (var line in infoLines) { Text(Left, y, line, false, 9, "0.200 0.250 0.330"); y -= 13; }
                y -= 6; firstPage = false;
            }
            else { Text(Left, y, title, true, 11, "0.094 0.204 0.357"); y -= 18; }
            // Encabezado de la tabla.
            var x = Left; const double hh = 20;
            page.Append("0.918 0.929 0.973 rg\n").Append(F(Left)).Append(' ').Append(F(y - hh)).Append(' ')
                .Append(F(widths.Sum())).Append(' ').Append(F(hh)).Append(" re f\n0 g 0.5 w\n");
            for (var c = 0; c < columns.Count; c++)
            {
                page.Append(F(x)).Append(' ').Append(F(y - hh)).Append(' ').Append(F(widths[c])).Append(' ').Append(F(hh)).Append(" re S\n");
                Text(x + 4, y - 14, columns[c], true, 9, "0.094 0.204 0.357"); x += widths[c];
            }
            y -= hh;
        }

        NewPage();
        foreach (var row in rows)
        {
            var wrapped = new List<string>[columns.Count]; var lines = 1;
            for (var c = 0; c < columns.Count; c++)
            {
                wrapped[c] = Wrap(row[c], (int)((widths[c] - 8) / (Font * 0.56)));
                lines = Math.Max(lines, wrapped[c].Count);
            }
            var rh = Math.Max(24, lines * 11 + 8);
            if (y - rh < Bottom) NewPage();
            var x = Left; page.Append("0 g 0.5 w\n");
            for (var c = 0; c < columns.Count; c++)
            {
                page.Append(F(x)).Append(' ').Append(F(y - rh)).Append(' ').Append(F(widths[c])).Append(' ').Append(F(rh)).Append(" re S\n");
                var ty = y - 15;
                foreach (var l in wrapped[c]) { Text(x + 4, ty, l, false, Font, "0.090 0.120 0.180"); ty -= 11; }
                x += widths[c];
            }
            y -= rh;
        }
        if (rows.Count == 0) Text(Left, y - 18, "Sin registros en este listado.", false, 10, "0.300 0.360 0.430");
        return Build(pages, footerNote);
    }

    private static List<string> Wrap(string input, int maxChars)
    {
        maxChars = Math.Max(6, maxChars);
        var printable = CandidateFormCapturePdf.Printable(input ?? "");
        var result = new List<string>(); var cur = new StringBuilder();
        foreach (var word in printable.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > maxChars) { if (cur.Length > 0) { result.Add(cur.ToString()); cur.Clear(); } result.Add(w[..maxChars]); w = w[maxChars..]; }
            if (cur.Length > 0 && cur.Length + 1 + w.Length > maxChars) { result.Add(cur.ToString()); cur.Clear(); }
            if (cur.Length > 0) cur.Append(' ');
            cur.Append(w);
        }
        if (cur.Length > 0) result.Add(cur.ToString());
        return result.Count == 0 ? [""] : result;
    }

    private static byte[] Build(IReadOnlyList<StringBuilder> pages, string footerNote)
    {
        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        var objects = new List<byte[]>
        {
            Array.Empty<byte>(), Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"), Array.Empty<byte>(),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>")
        };
        var refs = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            var pageNo = objects.Count; refs.Add($"{pageNo} 0 R");
            var body = pages[i] + "0.40 0.43 0.49 rg\nBT /F1 7 Tf 1 0 0 1 " + F(Left) + " 26 Tm <" +
                CandidateFormCapturePdf.HexWinAnsi(CandidateFormCapturePdf.Printable($"{footerNote} - Pagina {i + 1} de {pages.Count}")) + "> Tj ET\n";
            var stream = Encoding.ASCII.GetBytes(body);
            objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(W)} {F(H)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {pageNo + 1} 0 R >>"));
            using var block = new MemoryStream();
            block.Write(Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n")); block.Write(stream);
            block.Write(Encoding.ASCII.GetBytes("\nendstream")); objects.Add(block.ToArray());
        }
        objects[2] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", refs)}] >>");
        using var file = new MemoryStream();
        void Ascii(string s) => file.Write(Encoding.ASCII.GetBytes(s));
        Ascii("%PDF-1.4\n"); file.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);
        var offsets = new long[objects.Count];
        for (var id = 1; id < objects.Count; id++) { offsets[id] = file.Position; Ascii($"{id} 0 obj\n"); file.Write(objects[id]); Ascii("\nendobj\n"); }
        var xref = file.Position;
        Ascii($"xref\n0 {objects.Count}\n0000000000 65535 f \n");
        for (var id = 1; id < objects.Count; id++) Ascii($"{offsets[id]:D10} 00000 n \n");
        Ascii($"trailer\n<< /Size {objects.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return file.ToArray();
    }
}
