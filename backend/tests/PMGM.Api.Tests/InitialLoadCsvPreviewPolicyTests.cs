using System.Text.Json;
using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadCsvPreviewPolicyTests
{
    private static Dictionary<string, string> Synthetic()
    {
        var files = InitialLoadTemplateEndpoints.Sheets.ToDictionary(
            sheet => sheet.Name, sheet => string.Join(",", sheet.Columns) + "\r\n",
            StringComparer.Ordinal);
        files["Talleres.csv"] +=
            "taller23,23,\"Taller \"\"Sintético\"\"\",STGO,Santiago,Chile\r\n";
        files["Hermanos.csv"] +=
            "persona1,rut,12.345.678-5,\"Nombre, Sintético\",Prueba,qa@example.invalid,taller23,3,active\n";
        files["CargosVigentes.csv"] +=
            "persona1,taller23,worshipful_master,2026-01-01";
        return files;
    }

    [Fact]
    public void Valid_synthetic_csv_passes_static_rules_but_never_can_apply()
    {
        var result = InitialLoadCsvPreviewPolicy.Inspect(Synthetic());
        Assert.True(result.Valid, string.Join("; ", result.Findings.Select(x => x.Code)));
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
        Assert.Empty(result.Findings);
        Assert.All(result.DataRowCounts.Values, count => Assert.Equal(1, count));
        Assert.Equal(3, result.DataRowCounts.Count);
    }

    [Fact]
    public void Identity_error_reports_only_canonical_metadata()
    {
        var files = Synthetic();
        files["Hermanos.csv"] = files["Hermanos.csv"].Replace("12.345.678-5",
            "12.345.678-0", StringComparison.Ordinal);
        var result = InitialLoadCsvPreviewPolicy.Inspect(files);

        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Sheet == "Hermanos.csv" &&
            x.Field == "identityNumber" && x.Row == 2 && x.Code == "INVALID_IDENTITY");
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
        AssertNoPersonalValues(result);
    }

    [Fact]
    public void Broken_csv_cannot_reach_content_preflight()
    {
        var files = Synthetic();
        files["Hermanos.csv"] = "RUT,Email,Password\nprivate-secret";
        var result = InitialLoadCsvPreviewPolicy.Inspect(files);

        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Code == "HEADER_MISMATCH");
        Assert.DoesNotContain("private-secret", JsonSerializer.Serialize(result));
        AssertNoPersonalValues(result);
    }

    [Fact]
    public void Unknown_workshop_and_invalid_office_are_rejected_without_values()
    {
        var files = Synthetic();
        files["CargosVigentes.csv"] = files["CargosVigentes.csv"]
            .Replace("taller23,worshipful_master", "taller99,invalid-office",
                StringComparison.Ordinal);
        var result = InitialLoadCsvPreviewPolicy.Inspect(files);

        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Code == "UNKNOWN_WORKSHOP");
        Assert.Contains(result.Findings, x => x.Code == "PERSON_WORKSHOP_MISMATCH");
        Assert.Contains(result.Findings, x => x.Code == "UNKNOWN_OFFICE");
        Assert.DoesNotContain("taller99", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Duplicated_member_identity_cannot_pass_preflight()
    {
        var files = Synthetic();
        const string duplicate =
            "persona2,rut,12.345.678-5,Otra,Persona,other@example.invalid,taller23,2,active\n";
        files["Hermanos.csv"] += duplicate;
        var result = InitialLoadCsvPreviewPolicy.Inspect(files);

        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Field == "identityNumber" && x.Code == "DUPLICATE");
        Assert.Equal(2, result.DataRowCounts["Hermanos.csv"]);
        Assert.DoesNotContain("other@example.invalid", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Empty_template_headers_are_not_authorization_for_import()
    {
        var empty = InitialLoadTemplateEndpoints.Sheets.ToDictionary(
            sheet => sheet.Name, sheet => string.Join(",", sheet.Columns) + "\r\n",
            StringComparer.Ordinal);
        var result = InitialLoadCsvPreviewPolicy.Inspect(empty);
        Assert.True(result.Valid);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
        Assert.All(result.DataRowCounts.Values, n => Assert.Equal(0, n));
    }

    [Fact]
    public void Unknown_sheet_fails_closed_and_never_returns_its_name()
    {
        var files = Synthetic();
        files["Private RUT Documents.csv"] = "secret";
        var result = InitialLoadCsvPreviewPolicy.Inspect(files);
        Assert.False(result.Valid);
        Assert.False(result.CanApply);
        Assert.Contains(result.Findings, x => x.Code == "INVALID_SHEETS");
        Assert.DoesNotContain("Private RUT Documents", JsonSerializer.Serialize(result));
    }

    private static void AssertNoPersonalValues(InitialLoadCsvPreviewPolicy.Result result)
    {
        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("12.345.678", serialized);
        Assert.DoesNotContain("Nombre", serialized);
        Assert.DoesNotContain("Prueba", serialized);
        Assert.DoesNotContain("qa@example.invalid", serialized);
        Assert.DoesNotContain("persona1", serialized);
        Assert.DoesNotContain("taller23", serialized);
    }
}
