using HomeService.Domain.Enums;

namespace HomeService.Api;

public sealed class BusinessClientDocumentUploadService(
    IConfiguration configuration,
    IApiObjectStorage objectStorage)
{
    private const long MaxFileSize = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/pjpeg", "image/png", "image/webp",
        "image/heic", "image/heif", "application/octet-stream"
    };

    public async Task<StoredBusinessClientDocument> SaveAsync(
        Guid profileId,
        BusinessClientDocumentType documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Le fichier est vide.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Le fichier depasse la limite de 25 Mo.");
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(extension)
            || (!AllowedContentTypes.Contains(file.ContentType)
                && !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Seuls les fichiers PDF, JPG, PNG, WebP, HEIC et HEIF sont acceptes.");
        }

        var safeFileName = SanitizeFileName(originalFileName);
        var storagePath = Path.Combine(
                "business-clients",
                DateTime.UtcNow.ToString("yyyy"),
                DateTime.UtcNow.ToString("MM"),
                profileId.ToString("N"),
                documentType.ToString().ToLowerInvariant(),
                $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeFileName}")
            .Replace('\\', '/');

        await using var stream = file.OpenReadStream();
        await objectStorage.SaveAsync(
            ApiStorageVisibility.Private,
            GetDocumentsRoot(),
            storagePath,
            stream,
            file.ContentType,
            cancellationToken);

        return new StoredBusinessClientDocument(
            originalFileName,
            storagePath,
            file.ContentType,
            file.Length);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        objectStorage.OpenReadAsync(
            ApiStorageVisibility.Private,
            GetDocumentsRoot(),
            storagePath,
            cancellationToken);

    public Task DeleteIfExistsAsync(string storagePath, CancellationToken cancellationToken = default) =>
        objectStorage.DeleteIfExistsAsync(
            ApiStorageVisibility.Private,
            GetDocumentsRoot(),
            storagePath,
            cancellationToken);

    private string GetDocumentsRoot()
    {
        var configuredRoot = configuration["Storage:DocumentsRoot"];
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            configuredRoot = configuration["DOCUMENT_STORAGE_ROOT"];
        }

        return string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(AppContext.BaseDirectory, "storage", "documents")
            : configuredRoot;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Select(character => invalidChars.Contains(character) ? '-' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }
}

public sealed record StoredBusinessClientDocument(
    string OriginalFileName,
    string StoragePath,
    string ContentType,
    long Size);
