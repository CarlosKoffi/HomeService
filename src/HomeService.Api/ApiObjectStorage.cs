using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeService.Api;

public enum ApiStorageVisibility
{
    Public,
    Private
}

public interface IApiObjectStorage
{
    bool UsesR2 { get; }

    Task SaveAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        CancellationToken cancellationToken = default);

    string GetLocalAbsolutePath(string localRoot, string objectKey);

    string? GetPublicUrl(string objectKey);
}

public sealed class ApiObjectStorage : IApiObjectStorage, IDisposable
{
    private readonly ILogger<ApiObjectStorage> _logger;
    private readonly IAmazonS3? _r2Client;
    private readonly string? _publicBucket;
    private readonly string? _privateBucket;
    private readonly string? _publicBaseUrl;
    private readonly bool _publicDirectDeliveryEnabled;

    public ApiObjectStorage(IConfiguration configuration, ILogger<ApiObjectStorage>? logger = null)
    {
        _logger = logger ?? NullLogger<ApiObjectStorage>.Instance;

        var provider = FirstConfigured(
            configuration["STORAGE_PROVIDER"],
            configuration["Storage:Provider"]);
        _logger.LogWarning(
            "[STORAGE-DIAGNOSTIC] Storage configuration resolved. Provider={Provider}; AccountIdConfigured={AccountIdConfigured}; AccessKeyIdConfigured={AccessKeyIdConfigured}; SecretAccessKeyConfigured={SecretAccessKeyConfigured}; PublicBucketConfigured={PublicBucketConfigured}; PrivateBucketConfigured={PrivateBucketConfigured}.",
            provider ?? "<missing>",
            HasConfiguredValue(configuration, "Storage:R2:AccountId", "R2:AccountId", "R2_ACCOUNT_ID"),
            HasConfiguredValue(configuration, "Storage:R2:AccessKeyId", "R2:AccessKeyId", "R2_ACCESS_KEY_ID"),
            HasConfiguredValue(configuration, "Storage:R2:SecretAccessKey", "R2:SecretAccessKey", "R2_SECRET_ACCESS_KEY"),
            HasConfiguredValue(configuration, "Storage:R2:PublicBucket", "R2:PublicBucket", "R2_PUBLIC_BUCKET"),
            HasConfiguredValue(configuration, "Storage:R2:PrivateBucket", "R2:PrivateBucket", "R2_PRIVATE_BUCKET"));
        if (!string.Equals(provider, "R2", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[STORAGE-DIAGNOSTIC] R2 is disabled because STORAGE_PROVIDER resolved to {Provider} instead of R2.",
                provider ?? "<missing>");
            return;
        }

        var accountId = RequireConfiguration(configuration, "AccountId", "R2_ACCOUNT_ID", "R2:AccountId", "Storage:R2:AccountId");
        var accessKeyId = RequireConfiguration(configuration, "AccessKeyId", "R2_ACCESS_KEY_ID", "R2:AccessKeyId", "Storage:R2:AccessKeyId");
        var secretAccessKey = RequireConfiguration(configuration, "SecretAccessKey", "R2_SECRET_ACCESS_KEY", "R2:SecretAccessKey", "Storage:R2:SecretAccessKey");
        _publicBucket = RequireConfiguration(configuration, "PublicBucket", "R2_PUBLIC_BUCKET", "R2:PublicBucket", "Storage:R2:PublicBucket");
        _privateBucket = RequireConfiguration(configuration, "PrivateBucket", "R2_PRIVATE_BUCKET", "R2:PrivateBucket", "Storage:R2:PrivateBucket");
        _publicBaseUrl = NormalizeBaseUrl(FirstConfigured(
            configuration["R2_PUBLIC_BASE_URL"],
            configuration["R2:PublicBaseUrl"],
            configuration["Storage:R2:PublicBaseUrl"]));
        _publicDirectDeliveryEnabled = bool.TryParse(FirstConfigured(
            configuration["R2_PUBLIC_DIRECT_DELIVERY_ENABLED"],
            configuration["R2:PublicDirectDeliveryEnabled"],
            configuration["Storage:R2:PublicDirectDeliveryEnabled"]), out var directDeliveryEnabled)
            && directDeliveryEnabled;

        var endpoint = FirstConfigured(
            configuration["R2_ENDPOINT"],
            configuration["R2:Endpoint"],
            configuration["Storage:R2:Endpoint"])
            ?? $"https://{accountId}.r2.cloudflarestorage.com";

        _r2Client = new AmazonS3Client(
            new BasicAWSCredentials(accessKeyId, secretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "auto"
            });

        _logger.LogWarning(
            "[STORAGE-DIAGNOSTIC] Cloudflare R2 client enabled. PublicBucket={PublicBucket}; PrivateBucket={PrivateBucket}; PublicBaseUrlConfigured={PublicBaseUrlConfigured}; DirectDeliveryEnabled={DirectDeliveryEnabled}; EndpointHost={EndpointHost}.",
            _publicBucket,
            _privateBucket,
            !string.IsNullOrWhiteSpace(_publicBaseUrl),
            _publicDirectDeliveryEnabled,
            Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ? endpointUri.Host : "<invalid>");
    }

    public bool UsesR2 => _r2Client is not null;

    public async Task SaveAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeObjectKey(objectKey);
        if (_r2Client is null)
        {
            await SaveLocallyAsync(localRoot, normalizedKey, content, cancellationToken);
            return;
        }

        try
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            await _r2Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = ResolveBucket(visibility),
                Key = normalizedKey,
                InputStream = content,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                AutoCloseStream = false,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is AmazonS3Exception or AmazonServiceException or HttpRequestException)
        {
            _logger.LogError(exception, "Unable to store object {ObjectKey} in Cloudflare R2.", normalizedKey);
            throw new ApiObjectStorageException("Le stockage distant est momentanement indisponible.", exception);
        }
    }

    public async Task<Stream?> OpenReadAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeObjectKey(objectKey);
        if (_r2Client is not null)
        {
            try
            {
                var response = await _r2Client.GetObjectAsync(
                    ResolveBucket(visibility),
                    normalizedKey,
                    cancellationToken);
                return new OwnedResponseStream(response.ResponseStream, response);
            }
            catch (AmazonS3Exception exception) when (
                exception.StatusCode == HttpStatusCode.NotFound
                || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Object {ObjectKey} was not found in R2; checking legacy local storage.", normalizedKey);
            }
            catch (Exception exception) when (exception is AmazonS3Exception or AmazonServiceException or HttpRequestException)
            {
                _logger.LogError(exception, "Unable to read object {ObjectKey} from Cloudflare R2.", normalizedKey);
                throw new ApiObjectStorageException("Le fichier distant est momentanement indisponible.", exception);
            }
        }

        var absolutePath = GetLocalAbsolutePath(localRoot, normalizedKey);
        return File.Exists(absolutePath)
            ? new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous)
            : null;
    }

    public async Task<bool> ExistsAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeObjectKey(objectKey);
        if (_r2Client is null)
        {
            return File.Exists(GetLocalAbsolutePath(localRoot, normalizedKey));
        }

        try
        {
            await _r2Client.GetObjectMetadataAsync(
                ResolveBucket(visibility),
                normalizedKey,
                cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == HttpStatusCode.NotFound
            || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        catch (Exception exception) when (exception is AmazonS3Exception or AmazonServiceException or HttpRequestException)
        {
            _logger.LogError(exception, "Unable to check object {ObjectKey} in Cloudflare R2.", normalizedKey);
            throw new ApiObjectStorageException("Le stockage distant est momentanement indisponible.", exception);
        }
    }

    public async Task DeleteIfExistsAsync(
        ApiStorageVisibility visibility,
        string localRoot,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        var normalizedKey = NormalizeObjectKey(objectKey);
        if (_r2Client is not null)
        {
            try
            {
                await _r2Client.DeleteObjectAsync(
                    ResolveBucket(visibility),
                    normalizedKey,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is AmazonS3Exception or AmazonServiceException or HttpRequestException)
            {
                _logger.LogWarning(exception, "Unable to delete object {ObjectKey} from Cloudflare R2.", normalizedKey);
            }
        }

        try
        {
            var absolutePath = GetLocalAbsolutePath(localRoot, normalizedKey);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to delete legacy local object {ObjectKey}.", normalizedKey);
        }
    }

    public string GetLocalAbsolutePath(string localRoot, string objectKey)
    {
        var normalizedKey = NormalizeObjectKey(objectKey).Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(localRoot);
        var absolutePath = Path.GetFullPath(Path.Combine(root, normalizedKey));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!string.Equals(absolutePath, root, StringComparison.OrdinalIgnoreCase)
            && !absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chemin de stockage invalide.");
        }

        return absolutePath;
    }

    public string? GetPublicUrl(string objectKey)
    {
        if (_r2Client is null
            || !_publicDirectDeliveryEnabled
            || string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            return null;
        }

        var encodedPath = string.Join('/', NormalizeObjectKey(objectKey)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        return $"{_publicBaseUrl}/{encodedPath}";
    }

    public void Dispose()
    {
        _r2Client?.Dispose();
    }

    private string ResolveBucket(ApiStorageVisibility visibility)
    {
        return visibility == ApiStorageVisibility.Public ? _publicBucket! : _privateBucket!;
    }

    private async Task SaveLocallyAsync(
        string localRoot,
        string objectKey,
        Stream content,
        CancellationToken cancellationToken)
    {
        var absolutePath = GetLocalAbsolutePath(localRoot, objectKey);
        var temporaryPath = $"{absolutePath}.uploading-{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await content.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, absolutePath, overwrite: false);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new InvalidOperationException("Cle de stockage manquante.");
        }

        var normalized = objectKey.Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0
            || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")
            || Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Cle de stockage invalide.");
        }

        return normalized;
    }

    private static string RequireConfiguration(
        IConfiguration configuration,
        string settingName,
        params string[] keys)
    {
        var value = FirstConfigured(keys.Select(key => configuration[key]).ToArray());
        return value ?? throw new InvalidOperationException(
            $"Cloudflare R2 est active mais la configuration {settingName} est manquante.");
    }

    private static string? FirstConfigured(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static bool HasConfiguredValue(IConfiguration configuration, params string[] keys)
    {
        return keys.Any(key => !string.IsNullOrWhiteSpace(configuration[key]));
    }

    private static string? NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Storage:R2:PublicBaseUrl doit etre une URL HTTPS valide.");
        }

        return uri.ToString().TrimEnd('/');
    }
}

public sealed class ApiObjectStorageException(string message, Exception innerException)
    : IOException(message, innerException);

internal sealed class OwnedResponseStream(Stream innerStream, IDisposable owner) : Stream
{
    public override bool CanRead => innerStream.CanRead;
    public override bool CanSeek => innerStream.CanSeek;
    public override bool CanWrite => innerStream.CanWrite;
    public override long Length => innerStream.Length;
    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override void Flush() => innerStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => innerStream.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => innerStream.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        innerStream.ReadAsync(buffer, cancellationToken);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        innerStream.ReadAsync(buffer, offset, count, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
    public override void SetLength(long value) => innerStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => innerStream.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => innerStream.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        innerStream.WriteAsync(buffer, cancellationToken);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        innerStream.WriteAsync(buffer, offset, count, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
            owner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await innerStream.DisposeAsync();
        owner.Dispose();
        GC.SuppressFinalize(this);
    }
}
