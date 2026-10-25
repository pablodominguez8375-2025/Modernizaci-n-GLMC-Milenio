using System.Text.Json;
using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadCsvShapePolicyTests
{
    private static Dictionary<string, string> HeaderOnly()
        => InitialLoadTemplateEndpoints.Sheets.ToDictionary(
            s => s.Name, s => string.Join(",", s.Columns) + "\r\n",
            StringComparer.Ordinal);

    [Fact]
    public void All_three_header_only_templates_are_safe_but_never_applyable()
    {
        var scan = InitialLoadCsvShapePolicy.Inspect(HeaderOnly());
        Assert.True(scan.Valid);
        Assert.Equal(3, scan.DataRowCounts.Count);
        Assert.All(scan.DataRowCounts.Values, count => Assert.Equal(0, count));
        Assert.Empty(scan.Findings);
        Assert.False(scan.CanApply);
        Assert.Equal(0, scan.WritesPerformed);
    }

    [Fact]
    public void Supports_bom_crlf_and_single_line_escaped_quoted_values()
    {
        var files = HeaderOnly();
        files["Talleres.csv"] = "\uFEFF" + files["Talleres.csv"] +
            "t23,23,\"Taller \"\"Prueba\"\"\", STGO,Santiago,Chile\r\n";
        files["Hermanos.csv"] +=
            "h1,rut,12.345.678-5,\"Camila, Sintética\",Prueba,qa@example.invalid,t23,3,active\n";
        files["CargosVigentes.csv"] +=
            "h1,t23,worshipful_master,2026-01-01";
        var scan = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.True(scan.Valid, string.Join(";", scan.Findings.Select(x => x.Code)));
        Assert.All(scan.DataRowCounts.Values, count => Assert.Equal(1, count));
        var serialized = JsonSerializer.Serialize(scan);
        Assert.DoesNotContain("Camila", serialized);
        Assert.DoesNotContain("qa@example.invalid", serialized);
        Assert.DoesNotContain("12.345.678-5", serialized);
    }

    [Fact]
    public void Header_mismatch_rejects_without_echoing_unexpected_headers()
    {
        var files = HeaderOnly();
        files["Hermanos.csv"] = "SecretName,PrivateRUT,Email\n";
        var result = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Sheet == "Hermanos.csv" &&
            x.Code == "HEADER_MISMATCH" && x.Row == 1);
        Assert.DoesNotContain("SecretName", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("\"Private\"ignored", "INVALID_QUOTING")]
    [InlineData("\"Private\nText\"", "MULTILINE_NOT_SUPPORTED")]
    [InlineData("Private\rText", "INVALID_LINE_ENDING")]
    [InlineData("\"Unterminated", "UNTERMINATED_QUOTE")]
    public void Malformed_csv_fails_without_returning_private_values(string raw, string expected)
    {
        var files = HeaderOnly();
        files["Talleres.csv"] += raw;
        var result = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Code == expected);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Rejects_trailing_extra_column_and_oversized_cell()
    {
        var files = HeaderOnly();
        files["Talleres.csv"] += "t23,23,Taller,Santiago,Chile,X,EXTRA\n";
        var result = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Code == "COLUMN_COUNT");

        files = HeaderOnly();
        files["Talleres.csv"] += new string('X',
            InitialLoadCsvShapePolicy.MaxCharactersPerCell + 1);
        result = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Code == "FIELD_TOO_LONG");
        Assert.DoesNotContain(new string('X', 16), JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Rejects_oversized_files_and_too_many_rows_in_bounded_work()
    {
        var files = HeaderOnly();
        files["Hermanos.csv"] = new string('X',
            InitialLoadCsvShapePolicy.MaxCharactersPerFile + 1);
        var result = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.Contains(result.Findings, x => x.Code == "INVALID_FILE_SIZE");

        files = HeaderOnly();
        files["Talleres.csv"] += string.Concat(Enumerable.Repeat(
            "t23,23,Example,STGO,Santiago,Chile\n",
            InitialLoadCsvShapePolicy.MaxRowsPerSheet + 1));
        result = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.Contains(result.Findings, x => x.Code == "TOO_MANY_ROWS");
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Invalid_sheet_names_never_appear_in_diagnostic()
    {
        var files = HeaderOnly();
        files["Unauthorized Personal File.csv"] = "RUT;Name\n";
        var result = InitialLoadCsvShapePolicy.Inspect(files);
        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Code == "INVALID_SHEETS");
        Assert.DoesNotContain("Unauthorized", JsonSerializer.Serialize(result));
    }
}
