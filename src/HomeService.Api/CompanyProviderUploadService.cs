using HomeService.Domain.Enums;

namespace HomeService.Api;

public sealed class CompanyProviderUploadService
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
        "application/octet-stream",
        "application/pdf"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".heic",
        ".heif",
        ".pdf"
    };

    private readonly string _rootPath;
    private readonly IApiObjectStorage _objectStorage;

    public CompanyProviderUploadService(IConfiguration configuration, IApiObjectStorage? objectStorage = null)
    {
        _rootPath = configuration["Storage:RootPath"]
            ?? configuration["STORAGE_ROOT_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        _objectStorage = objectStorage ?? new ApiObjectStorage(configuration);
    }

    public async Task<IReadOnlyList<StoredCompanyProviderDocument>> SaveAsync(
        Guid companyId,
        Guid providerId,
        IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        var documents = new List<StoredCompanyProviderDocument>();

        foreach (var (fieldName, documentType) in GetDocumentFields())
        {
            var file = files.GetFile(fieldName);
            if (file is null || file.Length == 0)
            {
                continue;
            }

            if (file.Length > MaxFileSize)
            {
                throw new InvalidOperationException("Chaque fichier employe doit faire moins de 25 Mo.");
            }

            var safeExtension = ResolveSafeExtension(file, allowPdf: true);
            if (safeExtension is null)
            {
                throw new InvalidOperationException("Formats acceptes pour les employes: PDF, JPG, PNG, WEBP ou photo mobile HEIC.");
            }

            var originalFileName = NormalizeOriginalFileName(file.FileName, safeExtension);
            var relativePath = Path.Combine(
                "providers",
                companyId.ToString("D"),
                providerId.ToString("D"),
                $"{documentType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{safeExtension}");

            var storagePath = relativePath.Replace('\\', '/');
            await SaveFileAsync(storagePath, file, safeExtension, cancellationToken);

            documents.Add(new StoredCompanyProviderDocument(
                documentType,
                originalFileName,
                storagePath,
                NormalizeStoredContentType(file.ContentType, safeExtension)));
        }

        return documents;
    }

    public async Task<StoredCompanyProviderDocument> SaveOneAsync(
        Guid companyId,
        Guid providerId,
        ProviderDocumentType documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Le fichier employe est vide.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Chaque fichier employe doit faire moins de 25 Mo.");
        }

        var safeExtension = ResolveSafeExtension(file, allowPdf: documentType != ProviderDocumentType.Photo);
        if (safeExtension is null)
        {
            throw new InvalidOperationException(documentType == ProviderDocumentType.Photo
                ? "La photo de profil doit etre au format JPG, PNG, WEBP ou HEIC."
                : "Formats acceptes pour les employes: PDF, JPG, PNG, WEBP ou photo mobile HEIC.");
        }

        var originalFileName = NormalizeOriginalFileName(file.FileName, safeExtension);
        var relativePath = Path.Combine(
            "providers",
            companyId.ToString("D"),
            providerId.ToString("D"),
            $"{documentType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{safeExtension}");

        var storagePath = relativePath.Replace('\\', '/');
        await SaveFileAsync(storagePath, file, safeExtension, cancellationToken);

        return new StoredCompanyProviderDocument(
            documentType,
            originalFileName,
            storagePath,
            NormalizeStoredContentType(file.ContentType, safeExtension));
    }

    public Task<StoredCompanyProviderDocument> SaveMobileDocumentAsync(
        Guid providerId,
        ProviderDocumentType documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        return SaveProviderFileAsync(
            Path.Combine("providers", "mobile", providerId.ToString("D")),
            documentType.ToString().ToLowerInvariant(),
            documentType,
            file,
            cancellationToken);
    }

    public async Task<StoredProviderPortfolioFile> SavePortfolioImageAsync(
        Guid providerId,
        Guid serviceId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("La photo de book est vide.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Chaque photo de book doit faire moins de 25 Mo.");
        }

        var safeExtension = ResolveSafeExtension(file, allowPdf: false);
        if (safeExtension is null)
        {
            throw new InvalidOperationException("Formats acceptes pour le book: JPG, PNG, WEBP ou photo mobile HEIC.");
        }

        var originalFileName = NormalizeOriginalFileName(file.FileName, safeExtension);
        var relativePath = Path.Combine(
            "providers",
            "mobile",
            providerId.ToString("D"),
            "portfolio",
            serviceId.ToString("D"),
            $"book-{Guid.NewGuid():N}{safeExtension}");

        var storagePath = relativePath.Replace('\\', '/');
        await SaveFileAsync(storagePath, file, safeExtension, cancellationToken);

        return new StoredProviderPortfolioFile(
            originalFileName,
            storagePath,
            NormalizeStoredContentType(file.ContentType, safeExtension));
    }

    public string GetAbsolutePath(string relativePath)
    {
        return _objectStorage.GetLocalAbsolutePath(_rootPath, relativePath);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        _objectStorage.OpenReadAsync(ApiStorageVisibility.Private, _rootPath, storagePath, cancellationToken);

    public Task DeleteIfExistsAsync(string storagePath, CancellationToken cancellationToken = default) =>
        _objectStorage.DeleteIfExistsAsync(ApiStorageVisibility.Private, _rootPath, storagePath, cancellationToken);

    private static IEnumerable<(string FieldName, ProviderDocumentType DocumentType)> GetDocumentFields()
    {
        yield return ("photo", ProviderDocumentType.Photo);
        yield return ("identityDocument", ProviderDocumentType.IdentityDocument);
        yield return ("diplomaDocument", ProviderDocumentType.Diploma);
    }

    private async Task<StoredCompanyProviderDocument> SaveProviderFileAsync(
        string folder,
        string filePrefix,
        ProviderDocumentType documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Le fichier employe est vide.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidOperationException("Chaque fichier employe doit faire moins de 25 Mo.");
        }

        var safeExtension = ResolveSafeExtension(file, allowPdf: documentType != ProviderDocumentType.Photo);
        if (safeExtension is null)
        {
            throw new InvalidOperationException(documentType == ProviderDocumentType.Photo
                ? "La photo de profil doit etre au format JPG, PNG, WEBP ou HEIC."
                : "Formats acceptes pour les employes: PDF, JPG, PNG, WEBP ou photo mobile HEIC.");
        }

        var originalFileName = NormalizeOriginalFileName(file.FileName, safeExtension);
        var relativePath = Path.Combine(folder, $"{filePrefix}-{Guid.NewGuid():N}{safeExtension}");

        var storagePath = relativePath.Replace('\\', '/');
        await SaveFileAsync(storagePath, file, safeExtension, cancellationToken);

        return new StoredCompanyProviderDocument(
            documentType,
            originalFileName,
            storagePath,
            NormalizeStoredContentType(file.ContentType, safeExtension));
    }

    private async Task SaveFileAsync(
        string storagePath,
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        await _objectStorage.SaveAsync(
            ApiStorageVisibility.Private,
            _rootPath,
            storagePath,
            stream,
            NormalizeStoredContentType(file.ContentType, extension),
            cancellationToken);
    }

    private static string? ResolveSafeExtension(IFormFile file, bool allowPdf)
    {
        var contentType = file.ContentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        var contentTypeAllowed = AllowedContentTypes.Contains(contentType)
            || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        if (AllowedExtensions.Contains(extension)
            && contentTypeAllowed
            && (allowPdf || extension != ".pdf"))
        {
            return extension;
        }

        return contentType switch
        {
            "image/jpeg" or "image/pjpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            "application/pdf" when allowPdf => ".pdf",
            _ => null
        };
    }

    private static string NormalizeOriginalFileName(string fileName, string extension)
    {
        var safeName = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safeName) || string.IsNullOrWhiteSpace(Path.GetExtension(safeName))
            ? $"fichier-mobile{extension}"
            : safeName;
    }

    private static string NormalizeStoredContentType(string contentType, string extension)
    {
        var normalized = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (normalized != "application/octet-stream" && !string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}

public sealed record StoredCompanyProviderDocument(
    ProviderDocumentType DocumentType,
    string OriginalFileName,
    string StoragePath,
    string ContentType);

public sealed record StoredProviderPortfolioFile(
    string OriginalFileName,
    string StoragePath,
    string ContentType);
