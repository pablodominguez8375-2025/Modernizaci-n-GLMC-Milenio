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
    public void Credential_identity_and_contact_fields_are_removed_in_nested_collections()
    {
        var payload = new Dictionary<string, object?>
        {
            ["Status"] = "approved",
            ["SecretariatStatus"] = "reviewed",
            ["runId"] = 7,
            ["Access_Token"] = "synthetic-never-store",
            ["RUT"] = "synthetic-never-store",
            ["credentials"] = new Dictionary<string, object?>
            {
                ["Password"] = "synthetic-never-store",
                ["client-secret"] = "synthetic-never-store"
            },
            ["Events"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["api_key"] = "synthetic-never-store",
                    ["passport-number"] = "synthetic-never-store",
                    ["Email_Address"] = "synthetic-never-store",
                    ["phone_number"] = "synthetic-never-store",
                    ["card-number"] = "synthetic-never-store",
                    ["ResultCode"] = "DENIED"
                }
            }
        };

        using var json = JsonDocument.Parse(AuditMetadataSanitizer.Serialize(payload)!);
        var root = json.RootElement;
        Assert.Equal("approved", root.GetProperty("Status").GetString());
        Assert.Equal("reviewed", root.GetProperty("SecretariatStatus").GetString());
        Assert.Equal(7, root.GetProperty("runId").GetInt32());
        Assert.False(root.TryGetProperty("Access_Token", out _));
        Assert.False(root.TryGetProperty("RUT", out _));
        Assert.False(root.TryGetProperty("credentials", out _));
        var ev = root.GetProperty("Events")[0];
        Assert.False(ev.TryGetProperty("api_key", out _));
        Assert.False(ev.TryGetProperty("passport-number", out _));
        Assert.False(ev.TryGetProperty("Email_Address", out _));
        Assert.False(ev.TryGetProperty("phone_number", out _));
        Assert.False(ev.TryGetProperty("card-number", out _));
        Assert.Equal("DENIED", ev.GetProperty("ResultCode").GetString());
        Assert.DoesNotContain("synthetic-never-store", json.RootElement.ToString());
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Authorization-Header")]
    [InlineData("Proxy-Authorization")]
    [InlineData("authorization_code")]
    [InlineData("Refresh-Token")]
    [InlineData("pass_word")]
    [InlineData("biometric_template")]
    [InlineData("national-id")]
    [InlineData("date_of_birth")]
    [InlineData("private-key")]
    [InlineData("secret_key")]
    [InlineData("access-key")]
    [InlineData("encryption_key")]
    [InlineData("connectionString")]
    [InlineData("cvv")]
    public void Common_sensitive_aliases_are_never_kept(string key)
    {
        var metadata = new Dictionary<string, string> { [key] = "synthetic-never-store" };
        var result = AuditMetadataSanitizer.Serialize(metadata);
        Assert.NotNull(result);
        Assert.DoesNotContain(key, result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("synthetic-never-store", result);
    }

    [Fact]
    public void Institutional_authorization_proof_is_preserved_without_http_credentials()
    {
        // #470: an institutional digest is audit evidence, not a bearer secret.
        var payload = new Dictionary<string, object?>
        {
            ["sourceAuthorizationSnapshotSha256"] = "synthetic-provenance-digest",
            ["Status"] = "issued",
            ["Authorization"] = "Bearer synthetic-not-for-audit",
            ["Proxy-Authorization"] = "Basic synthetic-not-for-audit",
        };
        var serialized = AuditMetadataSanitizer.Serialize(payload);
        Assert.NotNull(serialized);
        using var doc = JsonDocument.Parse(serialized!);
        var root = doc.RootElement;
        Assert.Equal("synthetic-provenance-digest",
            root.GetProperty("sourceAuthorizationSnapshotSha256").GetString());
        Assert.Equal("issued", root.GetProperty("Status").GetString());
        Assert.False(root.TryGetProperty("Authorization", out _));
        Assert.False(root.TryGetProperty("Proxy-Authorization", out _));
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
    [InlineData("bad\r\nforged-event")]
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
