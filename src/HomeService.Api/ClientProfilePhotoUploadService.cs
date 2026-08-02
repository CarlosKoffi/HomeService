namespace HomeService.Api;

public sealed class ClientProfilePhotoUploadService(IConfiguration configuration)
{
    private const long MaxFileSize = 5 * 1024 * 1024;
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
            throw new InvalidOperationException("La photo doit faire entre 1 octet et 5 Mo.");
        }

        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Formats acceptes: JPG, PNG, WEBP ou HEIC.");
        }

        var relativePath = Path.Combine("client-profiles", customerId.ToString("N"), $"{Guid.NewGuid():N}{extension}");
        var absolutePath = GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);
        return relativePath.Replace('\\', '/');
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
