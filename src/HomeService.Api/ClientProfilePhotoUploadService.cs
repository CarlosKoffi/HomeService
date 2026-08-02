namespace HomeService.Api;

public sealed class ClientProfilePhotoUploadService(
    IConfiguration configuration,
    ILogger<ClientProfilePhotoUploadService> logger)
{
    private const long MaxFileSize = 25 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif"
    };

    private readonly string _rootPath = configuration["Storage:RootPath"]
        ?? configuration["STORAGE_ROOT_PATH"]
        ?? Path.Combine(AppContext.BaseDirectory, "storage");

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
        var absolutePath = GetAbsolutePath(relativePath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            await using var stream = new FileStream(
                absolutePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous);
            await file.CopyToAsync(stream, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return relativePath.Replace('\\', '/');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Unable to store client profile photo for customer {CustomerId} in {RootPath}", customerId, _rootPath);
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
        var root = Path.GetFullPath(_rootPath);
        var absolutePath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chemin de photo de profil invalide.");
        }

        return absolutePath;
    }
}

public sealed class ClientProfilePhotoStorageException(string message, Exception innerException)
    : Exception(message, innerException);
