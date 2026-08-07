using HomeService.Contracts.Clients;

namespace HomeService.Api;

public sealed class ClientMissionPhotoUploadService
{
    private const long MaxFileSize = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/pjpeg",
        "image/png",
        "image/webp",
        "image/heic",
        "image/heif",
        "application/octet-stream"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".heic",
        ".heif"
    };

    private readonly string _rootPath;
    private readonly IApiObjectStorage _objectStorage;

    public ClientMissionPhotoUploadService(IConfiguration configuration, IApiObjectStorage? objectStorage = null)
    {
        _rootPath = configuration["Storage:RootPath"]
            ?? configuration["STORAGE_ROOT_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        _objectStorage = objectStorage ?? new ApiObjectStorage(configuration);
    }

    public async Task<ClientMissionPhotoUploadResponse> SaveAsync(
        IFormFile file,
        string? caption,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("La photo client est vide.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Chaque photo client doit faire moins de 25 Mo.");
        }

        var safeExtension = ResolveExtension(file.FileName, file.ContentType);
        if (safeExtension is null)
        {
            throw new InvalidOperationException("Formats photos acceptes: JPG, PNG, WEBP ou photo mobile HEIC.");
        }

        var originalFileName = SanitizeFileName(file.FileName, safeExtension);
        var relativePath = Path.Combine(
            "client-missions",
            "pending",
            DateTimeOffset.UtcNow.ToString("yyyy"),
            DateTimeOffset.UtcNow.ToString("MM"),
            $"{Guid.NewGuid():N}{safeExtension}");

        var storagePath = relativePath.Replace('\\', '/');
        await using var stream = file.OpenReadStream();
        await _objectStorage.SaveAsync(
            ApiStorageVisibility.Private,
            _rootPath,
            storagePath,
            stream,
            NormalizeStoredContentType(file.ContentType, safeExtension),
            cancellationToken);

        return new ClientMissionPhotoUploadResponse(
            originalFileName,
            storagePath,
            NormalizeStoredContentType(file.ContentType, safeExtension),
            file.Length,
            Clean(caption, 500));
    }

    public string GetAbsolutePath(string relativePath)
    {
        return _objectStorage.GetLocalAbsolutePath(_rootPath, relativePath);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        _objectStorage.OpenReadAsync(ApiStorageVisibility.Private, _rootPath, storagePath, cancellationToken);

    private static string? ResolveExtension(string fileName, string contentType)
    {
        var normalizedContentType = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        if (AllowedExtensions.Contains(extension)
            && (AllowedContentTypes.Contains(normalizedContentType)
                || normalizedContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            return extension;
        }

        return normalizedContentType switch
        {
            "image/jpeg" or "image/pjpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            _ => null
        };
    }

    private static string SanitizeFileName(string fileName, string extension)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) || string.IsNullOrWhiteSpace(Path.GetExtension(safeName))
            ? $"photo-client{extension}"
            : safeName;
    }

    private static string NormalizeStoredContentType(string contentType, string extension)
    {
        var normalized = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return normalized == "application/octet-stream" || string.IsNullOrWhiteSpace(normalized)
            ? extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".heic" => "image/heic",
                ".heif" => "image/heif",
                _ => "image/jpeg"
            }
            : normalized;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
