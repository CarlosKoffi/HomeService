namespace HomeService.Api;

public sealed class ClientProfilePhotoUploadService
{
    private const long MaxFileSize = 25 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif"
    };

    private readonly string _rootPath;
    private readonly ILogger<ClientProfilePhotoUploadService> _logger;
    private readonly IApiObjectStorage _objectStorage;

    public ClientProfilePhotoUploadService(
        IConfiguration configuration,
        ILogger<ClientProfilePhotoUploadService> logger,
        IApiObjectStorage? objectStorage = null)
    {
        _rootPath = configuration["Storage:RootPath"]
            ?? configuration["STORAGE_ROOT_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        _logger = logger;
        _objectStorage = objectStorage ?? new ApiObjectStorage(configuration);
    }

    public async Task<string> SaveAsync(Guid customerId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("La photo doit faire entre 1 octet et 25 Mo.");
        }

        var extension = ResolveExtension(file.FileName, file.ContentType);
        if (extension is null)
        {
            throw new InvalidOperationException("Formats acceptes: JPG, PNG, WEBP ou HEIC.");
        }

        var relativePath = Path.Combine("client-profiles", customerId.ToString("N"), $"{Guid.NewGuid():N}{extension}");
        var storagePath = relativePath.Replace('\\', '/');
        try
        {
            await using var stream = file.OpenReadStream();
            await _objectStorage.SaveAsync(
                ApiStorageVisibility.Private,
                _rootPath,
                storagePath,
                stream,
                file.ContentType,
                cancellationToken);
            return storagePath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ApiObjectStorageException)
        {
            _logger.LogError(exception, "Unable to store client profile photo for customer {CustomerId}.", customerId);
            throw new ClientProfilePhotoStorageException(
                "Le stockage de la photo est momentanement indisponible. Reessayez dans quelques instants.",
                exception);
        }
    }

    private static string? ResolveExtension(string fileName, string contentType)
    {
        var normalizedContentType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        if (AllowedExtensions.Contains(extension)
            && (normalizedContentType.StartsWith("image/", StringComparison.Ordinal)
                || normalizedContentType == "application/octet-stream"))
        {
            return extension;
        }

        return normalizedContentType switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            _ => null
        };
    }

    public string GetAbsolutePath(string relativePath)
    {
        return _objectStorage.GetLocalAbsolutePath(_rootPath, relativePath);
    }

    public async Task DeleteIfExistsAsync(string? relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        try
        {
            await _objectStorage.DeleteIfExistsAsync(
                ApiStorageVisibility.Private,
                _rootPath,
                relativePath,
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Unable to delete obsolete client profile photo {RelativePath}.", relativePath);
        }
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        _objectStorage.OpenReadAsync(ApiStorageVisibility.Private, _rootPath, storagePath, cancellationToken);
}

public sealed class ClientProfilePhotoStorageException(string message, Exception innerException)
    : Exception(message, innerException);
