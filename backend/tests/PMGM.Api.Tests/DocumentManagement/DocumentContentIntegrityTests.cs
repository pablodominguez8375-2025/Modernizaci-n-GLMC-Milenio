using System.Security.Cryptography;
using System.Text;
using PMGM.Api.Modules.DocumentManagement;

namespace PMGM.Api.Tests.DocumentManagement;

public sealed class DocumentContentIntegrityTests
{
    [Fact]
    public async Task ComputeAsync_ReturnsSizeAndSha256()
    {
        var bytes = Encoding.UTF8.GetBytes("Proyecto Milenio");
        await using var stream = new MemoryStream(bytes);

        var result = await DocumentContentIntegrity.ComputeAsync(stream);

        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(bytes.LongLength, result.SizeBytes);
        Assert.Equal(expectedHash, result.Sha256);
        Assert.True(DocumentIntegrity.IsValidSha256(result.Sha256));
    }

    [Fact]
    public async Task ComputeAsync_EmptyStream_HasStableHash()
    {
        await using var stream = new MemoryStream(Array.Empty<byte>());

        var result = await DocumentContentIntegrity.ComputeAsync(stream);

        Assert.Equal(0, result.SizeBytes);
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            result.Sha256);
    }
}
