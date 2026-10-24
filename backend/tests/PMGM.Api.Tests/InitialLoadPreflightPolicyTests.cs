using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadPreflightPolicyTests
{
    private static Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> Example()
    {
        static IReadOnlyDictionary<string, string> Row(string file, params string[] fields)
        {
            var schema = InitialLoadTemplateEndpoints.Sheets.Single(s => s.Name == file);
            return schema.Columns.Zip(fields).ToDictionary(x => x.First, x => x.Second,
                StringComparer.Ordinal);
        }

        return new(StringComparer.Ordinal)
        {
            ["Talleres.csv"] =
            [
                Row("Talleres.csv", "taller23", "23", "Taller de prueba", "STGO",
                    "Santiago", "Chile")
            ],
            ["Hermanos.csv"] =
            [
                Row("Hermanos.csv", "hermano1", "rut", "12.345.678-5", "Ejemplo",
                    "Sintético", "qa@example.invalid", "taller23", "3", "active")
            ],
            ["CargosVigentes.csv"] =
            [
                Row("CargosVigentes.csv", "hermano1", "taller23", "worshipful_master",
                    "2026-01-01")
            ]
        };
    }

    private static void Set(
        Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> sheets,
        string file, string column, string value)
        => ((Dictionary<string, string>)sheets[file][0])[column] = value;

    [Fact]
    public void Fully_synthetic_canonical_rows_pass_only_static_preflight_never_apply()
    {
        var result = InitialLoadPreflightPolicy.Inspect(Example());
        Assert.True(result.Valid, string.Join(", ", result.Findings.Select(x => x.Code)));
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
        Assert.Empty(result.Findings);
    }

    [Theory]
    [InlineData("rut", "12.345.678-0", "INVALID_IDENTITY")]
    [InlineData("passport", "x", "INVALID_IDENTITY")]
    [InlineData("license", "123456", "INVALID_IDENTITY_TYPE")]
    public void Identity_errors_are_redacted_and_never_authorize_apply(
        string type, string value, string code)
    {
        var sheets = Example();
        Set(sheets, "Hermanos.csv", "identityType", type);
        Set(sheets, "Hermanos.csv", "identityNumber", value);
        var result = InitialLoadPreflightPolicy.Inspect(sheets);
        Assert.False(result.Valid);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
        Assert.Contains(result.Findings, x => x.Sheet == "Hermanos.csv" &&
            x.Row == 2 && x.Code == code);
        Assert.DoesNotContain(value, System.Text.Json.JsonSerializer.Serialize(result));
        Assert.DoesNotContain("Ejemplo", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.DoesNotContain("qa@example.invalid", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Rejects_missing_reference_cross_workshop_and_unknown_office()
    {
        var sheets = Example();
        Set(sheets, "CargosVigentes.csv", "personRef", "other");
        Set(sheets, "CargosVigentes.csv", "workshopCode", "taller99");
        Set(sheets, "CargosVigentes.csv", "officeCode", "nonexistent_role");
        Set(sheets, "CargosVigentes.csv", "startDate", "2026-02-30");
        var result = InitialLoadPreflightPolicy.Inspect(sheets);
        Assert.False(result.Valid);
        Assert.Contains(result.Findings, x => x.Code == "UNKNOWN_PERSON");
        Assert.Contains(result.Findings, x => x.Code == "UNKNOWN_WORKSHOP");
        Assert.Contains(result.Findings, x => x.Code == "UNKNOWN_OFFICE");
        Assert.Contains(result.Findings, x => x.Code == "INVALID_DATE");
        Assert.DoesNotContain("taller99", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Rejects_person_with_office_in_other_existing_workshop()
    {
        var sheets = Example();
        var workshops = sheets["Talleres.csv"].ToList();
        workshops.Add(InitialLoadTemplateEndpoints.Sheets[0].Columns
            .Zip(new[] { "taller99", "99", "Otro taller sintético", "STGO", "Santiago", "Chile" })
            .ToDictionary(x => x.First, x => x.Second));
        sheets["Talleres.csv"] = workshops;
        Set(sheets, "CargosVigentes.csv", "workshopCode", "taller99");
        var result = InitialLoadPreflightPolicy.Inspect(sheets);
        Assert.Contains(result.Findings, x => x.Code == "PERSON_WORKSHOP_MISMATCH");
    }

    [Fact]
    public void Duplicate_person_reference_and_identity_are_both_blocked()
    {
        var sheets = Example();
        sheets["Hermanos.csv"] =
        [
            sheets["Hermanos.csv"][0],
            new Dictionary<string, string>(sheets["Hermanos.csv"][0])
        ];
        var result = InitialLoadPreflightPolicy.Inspect(sheets);
        Assert.Contains(result.Findings, x => x.Code == "DUPLICATE" && x.Field == "personRef");
        Assert.Contains(result.Findings, x => x.Code == "DUPLICATE" && x.Field == "identityNumber");
        Assert.All(result.Findings, x => Assert.Equal("Hermanos.csv", x.Sheet));
    }

    [Fact]
    public void Rejects_unknown_columns_and_unknown_files_without_echoing_names()
    {
        var sheets = Example();
        ((Dictionary<string, string>)sheets["Hermanos.csv"][0])["secretPassword"] = "sensitive";
        var result = InitialLoadPreflightPolicy.Inspect(sheets);
        Assert.Contains(result.Findings, x => x.Code == "INVALID_COLUMNS");
        Assert.DoesNotContain("secretPassword", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.DoesNotContain("sensitive", System.Text.Json.JsonSerializer.Serialize(result));

        sheets["Untrusted Personal Files.csv"] = [];
        result = InitialLoadPreflightPolicy.Inspect(sheets);
        Assert.Contains(result.Findings, x => x.Code == "INVALID_SHEETS" && x.Sheet == "schema");
        Assert.DoesNotContain("Untrusted", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Denies_excess_rows_before_processing_any_person()
    {
        var sheets = Example();
        sheets["Hermanos.csv"] = Enumerable.Repeat(sheets["Hermanos.csv"][0],
            InitialLoadPreflightPolicy.MaxRowsPerSheet + 1).ToArray();
        var result = InitialLoadPreflightPolicy.Inspect(sheets);
        Assert.False(result.Valid);
        Assert.Single(result.Findings);
        Assert.Equal("INVALID_ROW_COUNT", result.Findings[0].Code);
        Assert.False(result.CanApply);
    }
}
