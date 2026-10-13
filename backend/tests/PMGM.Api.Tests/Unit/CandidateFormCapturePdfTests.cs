using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class CandidateFormCapturePdfTests
{
    private static CandidateFormCapture Row() => new()
    {
        CeremonyRequestId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        SchemaVersionId = Guid.NewGuid(),
        SchemaVersion = 1,
        ScopeKey = "order",
        CaptureRevision = 3,
        ValuesJson = "{}",
        PayloadSha256 = new string('a', 64),
        CapturedBySubject = "synthetic-user",
        CapturedAtUtc = new DateTimeOffset(2026, 10, 12, 14, 30, 0, TimeSpan.Zero)
    };

    private static FormSchemaSnapshot Schema() => new(
        "candidate_profile", 1,
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        FormSchemaInstitutionalCatalogTests.ValidFields());

    private static Dictionary<string, JsonElement> Values(string note) => new()
    {
        ["custom_note"] = JsonSerializer.SerializeToElement(note)
    };

    private static string PdfString(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static string ExtractVisibleText(byte[] bytes)
    {
        var pdf = PdfString(bytes);
        var lines = Regex.Matches(pdf, @"<([0-9A-F]+)> Tj")
            .Select(match => Encoding.Latin1.GetString(
                Convert.FromHexString(match.Groups[1].Value)));
        return string.Join(" ", lines);
    }

    [Fact]
    public void ProducesValidBasicPdfWithOriginalLabelsAndWithoutCorePersonalData()
    {
        var bytes = CandidateFormCapturePdf.Render(Row(), Schema(),
            Values("Motivación sintética con acentos: á é í ó ú ñ."));
        var source = PdfString(bytes);
        Assert.StartsWith("%PDF-1.4", source, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", source, StringComparison.Ordinal);
        Assert.Contains("/MediaBox [0 0 595.28 841.89]", source, StringComparison.Ordinal);
        Assert.Contains("/WinAnsiEncoding", source, StringComparison.Ordinal);
        Assert.Contains("/Type /Page /Parent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/JavaScript", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/URI ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/EmbeddedFile", source, StringComparison.Ordinal);

        var printable = ExtractVisibleText(bytes);
        Assert.Contains("Gran Logia Mixta de Chile", printable, StringComparison.Ordinal);
        Assert.Contains("Observación", printable, StringComparison.Ordinal);
        Assert.Contains("Motivación sintética con acentos: á é í ó ú ñ.", printable,
            StringComparison.Ordinal);
        Assert.Contains("Revisión: 3", printable, StringComparison.Ordinal);
        Assert.Contains("Clasificación: restricted", printable, StringComparison.Ordinal);
        Assert.DoesNotContain("rut_or_institutional_id", printable, StringComparison.Ordinal);
        Assert.DoesNotContain("first_names", printable, StringComparison.Ordinal);

        var marker = source.LastIndexOf("startxref\n", StringComparison.Ordinal);
        Assert.True(marker > 0);
        var indexStart = marker + "startxref\n".Length;
        var indexEnd = source.IndexOf('\n', indexStart);
        var offset = int.Parse(source[indexStart..indexEnd], CultureInfo.InvariantCulture);
        Assert.Equal("xref\n", source.Substring(offset, 5));
    }

    [Fact]
    public void ExplicitlyPreservesOutOfRangeUnicodeAsCodepointsInsteadOfDiscardingIt()
    {
        var pdf = CandidateFormCapturePdf.Render(Row(), Schema(),
            Values("Anotación ficticia 😀 文字"));
        var text = ExtractVisibleText(pdf);
        Assert.Contains("[U+1F600]", text, StringComparison.Ordinal);
        Assert.Contains("[U+6587]", text, StringComparison.Ordinal);
        Assert.Contains("[U+5B57]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LongNarrativeWrapsAcrossA4PagesWithoutDroppingContent()
    {
        var note = string.Join(" ", Enumerable.Repeat(
            "Comentario de prueba privado largo y no identificatorio para verificar paginación.", 190));
        var bytes = CandidateFormCapturePdf.Render(Row(), Schema(), Values(note));
        var source = PdfString(bytes);
        var pageCount = Regex.Matches(source, @"/Type /Page /Parent").Count;
        Assert.True(pageCount >= 3, $"Se esperaban al menos tres páginas; se generaron {pageCount}.");
        Assert.True(pageCount <= CandidateFormCapturePdf.MaxPages);
        var extracted = ExtractVisibleText(bytes);
        Assert.Contains("Documento reservado - Página 1 de", extracted, StringComparison.Ordinal);
        Assert.Contains($"Documento reservado - Página {pageCount} de {pageCount}",
            extracted, StringComparison.Ordinal);
        Assert.Contains("Observación", extracted, StringComparison.Ordinal);
        Assert.Contains("paginación.", extracted, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsVersionMismatchAndUnknownOrCoreValues()
    {
        Assert.Throws<InvalidDataException>(() => CandidateFormCapturePdf.Render(
            Row(), Schema() with { Version = 2 }, Values("Ficticio")));
        Assert.Throws<InvalidDataException>(() => CandidateFormCapturePdf.Render(
            Row(), Schema(), new Dictionary<string, JsonElement>
            {
                ["first_names"] = JsonSerializer.SerializeToElement("Identidad prohibida")
            }));
        Assert.Throws<InvalidDataException>(() => CandidateFormCapturePdf.Render(
            Row(), Schema(), new Dictionary<string, JsonElement>
            {
                ["unknown"] = JsonSerializer.SerializeToElement("No permitido")
            }));
    }
}
