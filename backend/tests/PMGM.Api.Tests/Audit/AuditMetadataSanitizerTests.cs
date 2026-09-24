using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
    public void Audit_correlation_id_accepts_safe_client_value()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "server-trace" };
        context.Request.Headers["X-Correlation-ID"] = "req-abc_1.x:2";

        var audit = AuditEventFactory.Create(context, "system.test", "Test", "1", null, AuditResults.Success);

        Assert.Equal("req-abc_1.x:2", audit.CorrelationId);
    }

    [Theory]
    [InlineData("bad\\r\\nforged-event")]
    [InlineData("")]
    public void Audit_correlation_id_uses_server_value_for_invalid_client_value(string supplied)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "server-trace" };
        context.Request.Headers["X-Correlation-ID"] = supplied;

        var audit = AuditEventFactory.Create(context, "system.test", "Test", "1", null, AuditResults.Success);

        Assert.Equal("server-trace", audit.CorrelationId);
    }

    [Fact]
    public void Audit_correlation_id_uses_server_value_when_client_value_is_too_long()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "server-trace" };
        context.Request.Headers["X-Correlation-ID"] = new string('a', 129);

        var audit = AuditEventFactory.Create(context, "system.test", "Test", "1", null, AuditResults.Success);

        Assert.Equal("server-trace", audit.CorrelationId);
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
