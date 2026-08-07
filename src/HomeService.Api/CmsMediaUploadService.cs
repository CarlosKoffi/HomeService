using HomeService.Domain.Entities;

namespace HomeService.Api;

public sealed class CmsMediaUploadService
{
    private const long MaxFileSize = 8 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private readonly string _rootPath;
    private readonly IApiObjectStorage _objectStorage;

    public CmsMediaUploadService(IConfiguration configuration, IApiObjectStorage? objectStorage = null)
    {
        _rootPath = configuration["Storage:RootPath"]
            ?? configuration["STORAGE_ROOT_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        _objectStorage = objectStorage ?? new ApiObjectStorage(configuration);
    }

    public async Task<CmsMediaAsset> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Le fichier image est vide.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Une image CMS doit faire moins de 8 Mo.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("Formats images acceptes: JPG, PNG, WEBP ou GIF.");
        }

        var extension = Path.GetExtension(file.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.ToLowerInvariant();
        var relativePath = Path.Combine(
            "cms",
            DateTimeOffset.UtcNow.ToString("yyyy"),
            DateTimeOffset.UtcNow.ToString("MM"),
            $"{Guid.NewGuid():N}{safeExtension}");

        var storagePath = relativePath.Replace('\\', '/');
        await using var stream = file.OpenReadStream();
        await _objectStorage.SaveAsync(
            ApiStorageVisibility.Public,
            _rootPath,
            storagePath,
            stream,
            file.ContentType,
            cancellationToken);

        var asset = new CmsMediaAsset(
            SanitizeFileName(file.FileName),
            storagePath,
            file.ContentType,
            file.Length);
        asset.MarkAvailable();

        return asset;
    }

    public string GetAbsolutePath(string relativePath)
    {
        return _objectStorage.GetLocalAbsolutePath(_rootPath, relativePath);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        _objectStorage.OpenReadAsync(ApiStorageVisibility.Public, _rootPath, storagePath, cancellationToken);

    public string? GetPublicUrl(string storagePath) => _objectStorage.GetPublicUrl(storagePath);

    private static string SanitizeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) ? "image-cms" : safeName;
    }
}
