using HomeService.Contracts.Clients;

namespace HomeService.Api;

public sealed class ClientMissionPhotoUploadService(IConfiguration configuration)
{
    private const long MaxFileSize = 5 * 1024 * 1024;

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

    private readonly string _rootPath = configuration["Storage:RootPath"]
        ?? configuration["STORAGE_ROOT_PATH"]
        ?? Path.Combine(AppContext.BaseDirectory, "storage");

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
            throw new InvalidOperationException("Chaque photo client doit faire moins de 5 Mo.");
        }

        if (!IsAllowedImage(file))
        {
            throw new InvalidOperationException("Formats photos acceptes: JPG, PNG, WEBP ou photo mobile HEIC.");
        }

        var originalFileName = SanitizeFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant();
        var relativePath = Path.Combine(
            "client-missions",
            "pending",
            DateTimeOffset.UtcNow.ToString("yyyy"),
            DateTimeOffset.UtcNow.ToString("MM"),
            $"{Guid.NewGuid():N}{safeExtension}");

        var absolutePath = GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        return new ClientMissionPhotoUploadResponse(
            originalFileName,
            relativePath.Replace('\\', '/'),
            file.ContentType,
            file.Length,
            Clean(caption, 500));
    }

    public string GetAbsolutePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var root = Path.GetFullPath(_rootPath);

        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chemin de photo client invalide.");
        }

        return absolutePath;
    }

    private static bool IsAllowedImage(IFormFile file)
    {
        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        return AllowedExtensions.Contains(extension)
            && (AllowedContentTypes.Contains(file.ContentType)
                || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) ? "photo-client.jpg" : safeName;
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
