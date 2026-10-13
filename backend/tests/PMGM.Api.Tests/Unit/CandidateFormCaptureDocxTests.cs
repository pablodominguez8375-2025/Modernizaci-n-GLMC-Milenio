using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class CandidateFormCaptureDocxTests
{
    private static readonly XNamespace Word =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static CandidateFormCapture Sample() => new()
    {
        CeremonyRequestId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        SchemaVersionId = Guid.NewGuid(),
        SchemaVersion = 1,
        ScopeKey = "order",
        CaptureRevision = 3,
        ValuesJson = "{}",
        PayloadSha256 = new string('a', 64),
        CapturedBySubject = "synthetic-test",
        CapturedAtUtc = new DateTimeOffset(2026, 10, 12, 14, 35, 0, TimeSpan.Zero)
    };

    private static FormSchemaSnapshot Schema() => new(
        "candidate_profile", 1,
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        FormSchemaInstitutionalCatalogTests.ValidFields());

    private static XDocument ReadBody(byte[] bytes)
    {
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        Assert.Equal(3, archive.Entries.Count);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("_rels/.rels"));
        var entry = archive.GetEntry("word/document.xml");
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        return XDocument.Load(stream);
    }

    [Fact]
    public void WritesInstitutionalVersionAndOriginalLabelsWithoutCanonicalIdentity()
    {
        var values = new Dictionary<string, JsonElement>
        {
            ["custom_note"] = JsonSerializer.SerializeToElement("Texto privado sintético con á é ñ.")
        };
        var document = ReadBody(CandidateFormCaptureDocx.Render(Sample(), Schema(), values));
        Assert.Equal(Word + "document", document.Root?.Name);
        var allText = string.Join(" ", document.Descendants(Word + "t").Select(x => x.Value));
        Assert.Contains("Observación", allText, StringComparison.Ordinal);
        Assert.Contains("Texto privado sintético con á é ñ.", allText, StringComparison.Ordinal);
        Assert.Contains("Revisión: 3", allText, StringComparison.Ordinal);
        Assert.Contains("Clasificación: restricted", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("rut_or_institutional_id", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("first_names", allText, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapesMarkupAndPreservesParagraphBreaks()
    {
        var document = ReadBody(CandidateFormCaptureDocx.Render(Sample(), Schema(),
            new Dictionary<string, JsonElement>
            {
                ["custom_note"] = JsonSerializer.SerializeToElement(
                    "Primera línea\nEtiqueta <ficticia> y & símbolo")
            }));
        var values = document.Descendants(Word + "t").Select(x => x.Value).ToArray();
        Assert.Contains(values, x => x.Contains("<ficticia>", StringComparison.Ordinal));
        Assert.Contains(values, x => x.Contains("& símbolo", StringComparison.Ordinal));
        Assert.True(document.Descendants(Word + "br").Any());
    }

    [Fact]
    public void RejectsHistoricalVersionMismatchAndUnmappedOrCoreFields()
    {
        Assert.Throws<InvalidDataException>(() => CandidateFormCaptureDocx.Render(
            Sample(), Schema() with { Version = 2 }, new Dictionary<string, JsonElement>()));
        Assert.Throws<InvalidDataException>(() => CandidateFormCaptureDocx.Render(
            Sample(), Schema(), new Dictionary<string, JsonElement>
            {
                ["first_names"] = JsonSerializer.SerializeToElement("No permitido")
            }));
        Assert.Throws<InvalidDataException>(() => CandidateFormCaptureDocx.Render(
            Sample(), Schema(), new Dictionary<string, JsonElement>
            {
                ["other_key"] = JsonSerializer.SerializeToElement("No permitido")
            }));
    }
}
