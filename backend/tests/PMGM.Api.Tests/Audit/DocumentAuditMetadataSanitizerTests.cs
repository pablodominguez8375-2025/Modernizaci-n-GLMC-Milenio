using System.Text.Json;
using PMGM.Api.Modules.Audit;
using Xunit;

namespace PMGM.Api.Tests.Audit;

public sealed class DocumentAuditMetadataSanitizerTests
{
    [Fact]
    public void Document_storage_and_content_identifiers_are_removed()
    {
        var json = AuditMetadataSanitizer.Serialize(new
        {
            Status = "available",
            Title = "Título reservado",
            OriginalFileName = "persona-documento.pdf",
            ObjectKey = "documents/secret/object",
            Sha256 = new string('a', 64),
            ScanReference = "scanner-secret-reference",
            VersionNumber = 2
        });

        Assert.NotNull(json);
        using var parsed = JsonDocument.Parse(json!);
        var root = parsed.RootElement;
        Assert.Equal("available", root.GetProperty("Status").GetString());
        Assert.Equal(2, root.GetProperty("VersionNumber").GetInt32());
        Assert.False(root.TryGetProperty("Title", out _));
        Assert.False(root.TryGetProperty("OriginalFileName", out _));
        Assert.False(root.TryGetProperty("ObjectKey", out _));
        Assert.False(root.TryGetProperty("Sha256", out _));
        Assert.False(root.TryGetProperty("ScanReference", out _));
    }
}
