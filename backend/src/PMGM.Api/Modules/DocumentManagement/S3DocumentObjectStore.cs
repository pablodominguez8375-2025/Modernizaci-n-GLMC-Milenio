using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace PMGM.Api.Modules.DocumentManagement;

public sealed class S3DocumentObjectStore : IDocumentObjectStore, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly DocumentStorageOptions _options;

    public S3DocumentObjectStore(IOptions<DocumentStorageOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Bucket))
            throw new InvalidOperationException("DocumentStorage:Bucket es obligatorio.");
        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException("Las credenciales de DocumentStorage son obligatorias.");

        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = _options.ForcePathStyle,
            AuthenticationRegion = string.IsNullOrWhiteSpace(_options.Region) ? "us-east-1" : _options.Region
        };

        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        _client = new AmazonS3Client(credentials, config);
    }

    public async Task StoreAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(objectKey, cancellationToken))
            throw new InvalidOperationException("El objeto documental ya existe y no puede sobrescribirse.");

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey
        }, cancellationToken);

        return new S3ObjectResponseStream(response);
    }

    public async Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Bucket,
                Key = objectKey
            }, cancellationToken);

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey
        }, cancellationToken);
    }

    public void Dispose() => _client.Dispose();

    private sealed class S3ObjectResponseStream(GetObjectResponse response) : Stream
    {
        private Stream Inner => response.ResponseStream;

        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => Inner.Length;
        public override long Position
        {
            get => Inner.Position;
            set => Inner.Position = value;
        }

        public override void Flush() => Inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => Inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => Inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) response.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            response.Dispose();
            await base.DisposeAsync();
        }
    }
}
