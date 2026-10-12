using System.Text;
using System.Text.Json;
using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class CandidateFormCaptureCsvTests
{
    private static CandidateFormCapture Row(int version = 1) => new()
    {
        CeremonyRequestId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        SchemaVersionId = Guid.NewGuid(),
        SchemaVersion = version,
        ScopeKey = "order",
        CaptureRevision = 2,
        ValuesJson = "{}",
        PayloadSha256 = new string('a', 64),
        CapturedBySubject = "synthetic-test",
        CapturedAtUtc = new DateTimeOffset(2026, 10, 12, 14, 30, 0, TimeSpan.Zero)
    };

    private static FormSchemaSnapshot Schema(int version = 1)
        => new("candidate_profile", version,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            FormSchemaInstitutionalCatalogTests.ValidFields());

    [Fact]
    public void ExportsPrivateFieldWithOriginalLabelAndNoCanonicalIdentity()
    {
        var values = new Dictionary<string, JsonElement>
        {
            ["custom_note"] = JsonSerializer.SerializeToElement("Motivación completamente ficticia")
        };
        var csv = Encoding.UTF8.GetString(CandidateFormCaptureCsv.Render(Row(), Schema(), values));
        Assert.StartsWith("\uFEFF", csv, StringComparison.Ordinal);
        Assert.Contains("\"Observación\";\"custom_note\";\"Motivación completamente ficticia\";", csv,
            StringComparison.Ordinal);
        Assert.Contains(";\"restricted\";\"multiline\";\"1\";\"2\";", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("rut_or_institutional_id", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Nombres\";", csv, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"https://invalid.test\",\"engaño\")")]
    [InlineData("   +SUM(1,2)")]
    [InlineData("\t@IMPORTXML(1)")]
    [InlineData("\r-1+2")]
    [InlineData("\0=cmd")]
    [InlineData("\u200B=HYPERLINK(1)")]
    public void NeutralizesSpreadsheetFormulaPayloadsWithoutChangingSource(string raw)
    {
        var values = new Dictionary<string, JsonElement>
        {
            ["custom_note"] = JsonSerializer.SerializeToElement(raw)
        };
        var csv = Encoding.UTF8.GetString(CandidateFormCaptureCsv.Render(Row(), Schema(), values));
        Assert.Contains(";\"'" + raw.Replace("\"", "\"\"", StringComparison.Ordinal) + "\";",
            csv, StringComparison.Ordinal);
        Assert.Equal(raw, values["custom_note"].GetString());
    }

    [Fact]
    public void QuotesNewlinesDoubleQuotesAndSeparators()
    {
        const string value = "Primer párrafo;\"dentro\"\nSegundo párrafo";
        var csv = Encoding.UTF8.GetString(CandidateFormCaptureCsv.Render(Row(), Schema(),
            new Dictionary<string, JsonElement>
            {
                ["custom_note"] = JsonSerializer.SerializeToElement(value)
            }));
        Assert.Contains("\"Primer párrafo;\"\"dentro\"\"\nSegundo párrafo\"", csv,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesMismatchedSchemaAndUnmappedPrivateValues()
    {
        var capture = Row();
        Assert.Throws<InvalidDataException>(() => CandidateFormCaptureCsv.Render(capture,
            Schema(2), new Dictionary<string, JsonElement>()));
        Assert.Throws<InvalidDataException>(() => CandidateFormCaptureCsv.Render(capture,
            Schema(), new Dictionary<string, JsonElement>
            {
                ["first_names"] = JsonSerializer.SerializeToElement("No exportar identidad"),
            }));
        Assert.Throws<InvalidDataException>(() => CandidateFormCaptureCsv.Render(capture,
            Schema(), new Dictionary<string, JsonElement>
            {
                ["unmapped_private_key"] = JsonSerializer.SerializeToElement("dato"),
            }));
    }
}
