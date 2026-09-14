using System.Text.Json;
using PMGM.Api.Modules.Audit;
using Xunit;

namespace PMGM.Api.Tests.Audit;

public sealed class AuditMetadataSanitizerTests
{
    [Fact]
    public void Sensitive_identifiers_and_lodge_content_are_removed_recursively()
    {
        var json = AuditMetadataSanitizer.Serialize(new
        {
            MeetingDate = "2026-09-08",
            Grade = "master",
            MemberId = Guid.NewGuid(),
            Nested = new
            {
                Content = "Texto de acta restringido",
                ExcuseReason = "Antecedente privado",
                Status = "approved"
            }
        });

        Assert.NotNull(json);
        using var document = JsonDocument.Parse(json!);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("MeetingDate", out _));
        Assert.False(root.TryGetProperty("Grade", out _));
        Assert.False(root.TryGetProperty("MemberId", out _));
        var nested = root.GetProperty("Nested");
        Assert.False(nested.TryGetProperty("Content", out _));
        Assert.False(nested.TryGetProperty("ExcuseReason", out _));
        Assert.Equal("approved", nested.GetProperty("Status").GetString());
    }

    [Fact]
    public void Operational_non_sensitive_metadata_is_preserved()
    {
        var json = AuditMetadataSanitizer.Serialize(new
        {
            Status = "reserved",
            StartsAtUtc = "2026-09-08T18:00:00Z",
            Reason = "schedule_conflict"
        });

        Assert.Contains("reserved", json);
        Assert.Contains("schedule_conflict", json);
    }
}
