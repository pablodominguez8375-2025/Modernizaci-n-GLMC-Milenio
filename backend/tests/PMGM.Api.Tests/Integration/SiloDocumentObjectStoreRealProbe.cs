using System.Net;
using Amazon.S3;
using Microsoft.Extensions.Options;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// True Silo + Amazon SDK .NET compatibility. Requires an explicitly provisioned
/// ephemeral test environment (PMGM_TEST_REQUIRE_REAL_SILO=1) and never uses
/// production credentials, documents, or infrastructure.
/// </summary>
public sealed class SiloDocumentObjectStoreRealIntegrationTests
{
    [Fact]
    public async Task Restricted_Silo_identity_operates_document_store_and_rejects_other_buckets()
    {
        // This exact test runs only in the isolated lab invoked by CI.
        // Otherwise no Docker/Silo is assumed for regular .NET suites.
        if (Environment.GetEnvironmentVariable("PMGM_TEST_REQUIRE_REAL_SILO") != "1")
            return;

        string Required(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException($"Falta configuración sintética {key}.");
        }

        var url = Required("PMGM_TEST_S3_URL");
        var user = Required("PMGM_TEST_S3_ACCESS_KEY");
        var secret = Required("PMGM_TEST_S3_SECRET_KEY");
        var allowedBucket = Required("PMGM_TEST_S3_BUCKET");
        var deniedBucket = Required("PMGM_TEST_S3_FORBIDDEN_BUCKET");

        Assert.Equal("pmgm-documents", allowedBucket);
        Assert.NotEqual(allowedBucket, deniedBucket);
        Assert.StartsWith("http://", url, StringComparison.Ordinal);

        DocumentStorageOptions OptionsFor(string bucket) => new()
        {
            ServiceUrl = url,
            Bucket = bucket,
            AccessKey = user,
            SecretKey = secret,
            Region = "us-east-1",
            ForcePathStyle = true
        };

        var ct = TestContext.Current.CancellationToken;
        using var store = new S3DocumentObjectStore(Options.Create(OptionsFor(allowedBucket)));
        using var forbidden = new S3DocumentObjectStore(Options.Create(OptionsFor(deniedBucket)));
        var key = "ci/dotnet/synthetic-" + Guid.NewGuid().ToString("N") + ".bin";
        byte[] payload = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32 * 1024);
        var expectedHash = System.Security.Cryptography.SHA256.HashData(payload);
        var created = false;

        try
        {
            Assert.False(await store.ExistsAsync(key, ct));

            await using (var upload = new MemoryStream(payload, writable: false))
            {
                await store.StoreAsync(key, upload, "application/octet-stream", ct);
                created = true;
            }

            Assert.True(await store.ExistsAsync(key, ct));
            await using (var response = await store.OpenReadAsync(key, ct))
            await using (var restored = new MemoryStream())
            {
                await response.CopyToAsync(restored, ct);
                Assert.Equal(payload, restored.ToArray());
                Assert.Equal(expectedHash,
                    System.Security.Cryptography.SHA256.HashData(restored.ToArray()));
            }

            await using var duplicate = new MemoryStream(payload, writable: false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.StoreAsync(key, duplicate, "application/octet-stream", ct));

            // The same actual production S3 client must reject other buckets.
            var restrictedKey = "ci/dotnet/forbidden-" + Guid.NewGuid().ToString("N");
            var deniedGet = await Assert.ThrowsAsync<AmazonS3Exception>(
                () => forbidden.OpenReadAsync(restrictedKey, ct));
            Assert.Equal(HttpStatusCode.Forbidden, deniedGet.StatusCode);
            var deniedHead = await Assert.ThrowsAsync<AmazonS3Exception>(
                () => forbidden.ExistsAsync(restrictedKey, ct));
            Assert.Equal(HttpStatusCode.Forbidden, deniedHead.StatusCode);
            var deniedDelete = await Assert.ThrowsAsync<AmazonS3Exception>(
                () => forbidden.DeleteAsync(restrictedKey, ct));
            Assert.Equal(HttpStatusCode.Forbidden, deniedDelete.StatusCode);
            await using var deniedUpload = new MemoryStream(payload, writable: false);
            var deniedPut = await Assert.ThrowsAsync<AmazonS3Exception>(
                () => forbidden.StoreAsync(restrictedKey, deniedUpload, "application/octet-stream", ct));
            Assert.Equal(HttpStatusCode.Forbidden, deniedPut.StatusCode);
        }
        finally
        {
            if (created)
                await store.DeleteAsync(key, ct);
        }

        Assert.False(await store.ExistsAsync(key, ct));
    }
}
