using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.Audit;
using Xunit;

namespace PMGM.Api.Tests.Integration;

public sealed class QaSyntheticPlanContractTests
{
    private static string ValidPayload => """
    {
      "schema": "PMGM-QA-OPERATIVE-DATASET-v1",
      "classification": "SYNTHETIC_ONLY",
      "importStatus": "NOT_IMPORTED",
      "preflightStatus": "READY_FOR_MAPPING_ONLY",
      "sourceSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
      "writesPerformed": 0,
      "mapped": { "Organization": 4, "Person": 12, "Member": 12, "Membership": 12 },
      "unmapped": {
        "offices": 3, "quotas": 9, "cash_receipts": 7,
        "quota_allocations": 7, "hospitalaria": 2, "meetings": 5, "attendance": 11
      }
    }
    """;

    private static bool Accepts(string content)
    {
        using var document = JsonDocument.Parse(content);
        return QaSyntheticPlanEndpoints.TrySummarize(document.RootElement, out _);
    }

    [Fact]
    public void Valid_summary_is_always_untrusted_and_contains_only_counts()
    {
        using var document = JsonDocument.Parse(ValidPayload);
        Assert.True(QaSyntheticPlanEndpoints.TrySummarize(document.RootElement, out var plan));
        Assert.NotNull(plan);
        Assert.Equal(4, plan!.Mapped["Organization"]);
        Assert.Equal(12, plan.Mapped["Membership"]);
        Assert.Equal(7, plan.NotMapped["cash_receipts"]);
        Assert.DoesNotContain("email", JsonSerializer.Serialize(plan), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Production", "true", false)]
    [InlineData("Development", "true", false)]
    [InlineData("Testing", "true", false)]
    [InlineData("QA", "false", false)]
    [InlineData("QA", "true", true)]
    public void Plan_is_opt_in_and_qa_only(string environment, string switchValue, bool expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["QaSyntheticImport:PlanEnabled"] = switchValue })
            .Build();
        Assert.Equal(expected, QaSyntheticPlanEndpoints.PlanIsEnabled(environment, config));
    }

    [Fact]
    public void Rejects_additional_fields_or_duplicate_keys_in_all_objects()
    {
        const string originalWrites = "\"writesPerformed\": 0";
        Assert.False(Accepts(ValidPayload.Replace(
            originalWrites, "\"writesPerformed\": 0, \"rut\": \"12345678-9\"", StringComparison.Ordinal)));
        Assert.False(Accepts(ValidPayload.Replace(
            "\"Person\": 12", "\"Person\": 12, \"Person\": 12", StringComparison.Ordinal)));
        Assert.False(Accepts(ValidPayload.Replace(
            "\"offices\": 3", "\"offices\": 3, \"privateNotes\": \"secreto\"", StringComparison.Ordinal)));
        Assert.False(Accepts(ValidPayload.Replace(
            "\"sourceSha256\": \"aaaaaaaa", "\"sourceSha256\": \"INVALID_HASH", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("\"writesPerformed\": 0", "\"writesPerformed\": 1")]
    [InlineData("\"writesPerformed\": 0", "\"writesPerformed\": false")]
    [InlineData("\"writesPerformed\": 0", "\"writesPerformed\": 0.0")]
    [InlineData("\"Organization\": 4", "\"Organization\": 4.0")]
    [InlineData("\"Person\": 12", "\"Person\": 12e0")]
    [InlineData("\"Member\": 12", "\"Member\": 13")]
    [InlineData("\"Organization\": 4", "\"Organization\": 31")]
    [InlineData("\"classification\": \"SYNTHETIC_ONLY\"", "\"classification\": \"PERSONAL_REAL\"")]
    [InlineData("\"importStatus\": \"NOT_IMPORTED\"", "\"importStatus\": \"IMPORTED\"")]
    [InlineData("\"preflightStatus\": \"READY_FOR_MAPPING_ONLY\"", "\"preflightStatus\": \"IMPORT_COMPLETE\"")]
    public void Rejects_unsafe_metadata(string from, string to)
    {
        Assert.Contains(from, ValidPayload, StringComparison.Ordinal);
        Assert.False(Accepts(ValidPayload.Replace(from, to, StringComparison.Ordinal)));
    }

    [Fact]
    public void Rejects_array_payload_and_no_contract_reports_imported()
    {
        Assert.False(Accepts("[]"));
        Assert.False(Accepts("{ }"));
        Assert.False(Accepts(ValidPayload.Replace(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "INVALID_HASH", StringComparison.Ordinal)));
    }
}
