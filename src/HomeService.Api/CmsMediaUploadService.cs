using HomeService.Domain.Entities;

namespace HomeService.Api;

public sealed class CmsMediaUploadService(IConfiguration configuration)
{
    private const long MaxFileSize = 8 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private readonly string _rootPath = configuration["Storage:RootPath"]
        ?? configuration["STORAGE_ROOT_PATH"]
        ?? Path.Combine(AppContext.BaseDirectory, "storage");

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

        var absolutePath = GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        var asset = new CmsMediaAsset(
            SanitizeFileName(file.FileName),
            relativePath.Replace('\\', '/'),
            file.ContentType,
            file.Length);
        asset.MarkAvailable();

        return asset;
    }

    public string GetAbsolutePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var root = Path.GetFullPath(_rootPath);

        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chemin media CMS invalide.");
        }

        return absolutePath;
    }

    private static string SanitizeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) ? "image-cms" : safeName;
    }
}
