using System.Security.Cryptography;

namespace PMGM.Api.Modules.DocumentManagement;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "pmgm-documents";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; } = true;
    public long MaxUploadBytes { get; set; } = 52_428_800;
}

public sealed class DocumentMalwareOptions
{
    public const string SectionName = "DocumentMalware";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public long MaxStreamBytes { get; set; } = 52_428_800;
}

public sealed record DocumentContentIntegrityResult(long SizeBytes, string Sha256);

public static class DocumentContentIntegrity
{
    public static async Task<DocumentContentIntegrityResult> ComputeAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long total = 0;

        while (true)
        {
            var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;

            total = checked(total + read);
            hash.AppendData(buffer, 0, read);
        }

        return new DocumentContentIntegrityResult(
            total,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }
}
