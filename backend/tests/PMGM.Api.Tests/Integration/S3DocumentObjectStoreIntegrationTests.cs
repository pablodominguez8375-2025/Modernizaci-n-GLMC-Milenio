using Microsoft.Extensions.Options;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

public sealed class S3DocumentObjectStoreIntegrationTests
{
    [Fact]
    public async Task Store_open_exists_and_delete_roundtrip_against_s3_compatible_storage()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("PMGM_TEST_S3_URL");
        var accessKey = Environment.GetEnvironmentVariable("PMGM_TEST_S3_ACCESS_KEY");
        var secretKey = Environment.GetEnvironmentVariable("PMGM_TEST_S3_SECRET_KEY");
        var bucket = Environment.GetEnvironmentVariable("PMGM_TEST_S3_BUCKET");

        if (string.IsNullOrWhiteSpace(serviceUrl) ||
            string.IsNullOrWhiteSpace(accessKey) ||
            string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(bucket))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = Options.Create(new DocumentStorageOptions
        {
            ServiceUrl = serviceUrl,
            AccessKey = accessKey,
            SecretKey = secretKey,
            Bucket = bucket,
            Region = "us-east-1",
            ForcePathStyle = true
        });

        using var store = new S3DocumentObjectStore(options);
        var key = $"ci/object-store/{Guid.NewGuid():N}";
        var payload = "Proyecto Milenio — S3 integration"u8.ToArray();

        try
        {
            await using (var upload = new MemoryStream(payload, writable: false))
            {
                await store.StoreAsync(key, upload, "text/plain", cancellationToken);
            }

            Assert.True(await store.ExistsAsync(key, cancellationToken));

            await using (var download = await store.OpenReadAsync(key, cancellationToken))
            await using (var buffer = new MemoryStream())
            {
                await download.CopyToAsync(buffer, cancellationToken);
                Assert.Equal(payload, buffer.ToArray());
            }

            await using var duplicate = new MemoryStream(payload, writable: false);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.StoreAsync(key, duplicate, "text/plain", cancellationToken));
        }
        finally
        {
            await store.DeleteAsync(key, cancellationToken);
        }

        Assert.False(await store.ExistsAsync(key, cancellationToken));
    }
}
