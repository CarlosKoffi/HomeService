using HomeService.Domain.Enums;

namespace HomeService.Api;

public sealed class CompanyApplicationUploadService
{
    private const long MaxFileSize = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
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
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".heic",
        ".heif"
    };

    private static readonly Dictionary<string, CompanyDocumentType> DocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fiscalExistenceDeclaration"] = CompanyDocumentType.FiscalExistenceDeclaration,
        ["companyDocument"] = CompanyDocumentType.BusinessRegistration,
        ["ownerIdentityDocument"] = CompanyDocumentType.OwnerIdentity,
        ["addressProof"] = CompanyDocumentType.AddressProof,
        ["supportingDocument"] = CompanyDocumentType.SupportingDocument
    };

    private static readonly Dictionary<string, ProviderDocumentType> ProviderDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["photo"] = ProviderDocumentType.Photo,
        ["identityDocument"] = ProviderDocumentType.IdentityDocument,
        ["diploma"] = ProviderDocumentType.Diploma
    };

    private readonly IConfiguration _configuration;
    private readonly IApiObjectStorage _objectStorage;

    public CompanyApplicationUploadService(IConfiguration configuration, IApiObjectStorage? objectStorage = null)
    {
        _configuration = configuration;
        _objectStorage = objectStorage ?? new ApiObjectStorage(configuration);
    }

    public async Task<IReadOnlyList<StoredCompanyApplicationDocument>> SaveAsync(
        Guid companyApplicationId,
        IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        var storedDocuments = new List<StoredCompanyApplicationDocument>();

        foreach (var file in files)
        {
            if (!DocumentTypes.TryGetValue(file.Name, out var documentType) || file.Length == 0)
            {
                continue;
            }

            if (file.Length > MaxFileSize)
            {
                throw new InvalidOperationException($"Le fichier {file.FileName} depasse la limite de 25 Mo.");
            }

            var originalFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalFileName);
            if (!IsAllowedFile(file, extension))
            {
                throw new InvalidOperationException($"Le format du fichier {file.FileName} n'est pas accepte.");
            }

            var relativeDirectory = Path.Combine(
                "company-applications",
                DateTime.UtcNow.ToString("yyyy"),
                DateTime.UtcNow.ToString("MM"),
                companyApplicationId.ToString("N"),
                ToFolderName(documentType));
            var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{SanitizeFileName(originalFileName)}";
            var relativePath = Path.Combine(relativeDirectory, safeFileName).Replace('\\', '/');
            await SaveFileAsync(relativePath, file, cancellationToken);
            storedDocuments.Add(new StoredCompanyApplicationDocument(
                documentType,
                originalFileName,
                relativePath,
                file.ContentType));
        }

        return storedDocuments;
    }

    public async Task<StoredProviderDocument> SaveProviderDocumentAsync(
        Guid providerId,
        string formFieldName,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!ProviderDocumentTypes.TryGetValue(formFieldName, out var documentType) || file.Length == 0)
        {
            throw new InvalidOperationException("Type de document employe invalide.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new InvalidOperationException($"Le fichier {file.FileName} depasse la limite de 25 Mo.");
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        if (!IsAllowedFile(file, extension))
        {
            throw new InvalidOperationException($"Le format du fichier {file.FileName} n'est pas accepte.");
        }

        var relativeDirectory = Path.Combine(
            "providers",
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            providerId.ToString("N"),
            ToProviderFolderName(documentType));
        var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{SanitizeFileName(originalFileName)}";
        var relativePath = Path.Combine(relativeDirectory, safeFileName).Replace('\\', '/');
        await SaveFileAsync(relativePath, file, cancellationToken);
        return new StoredProviderDocument(documentType, originalFileName, relativePath, file.ContentType);
    }

    public string GetAbsolutePath(string storagePath)
    {
        return _objectStorage.GetLocalAbsolutePath(GetDocumentsRoot(), storagePath);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken) =>
        _objectStorage.OpenReadAsync(
            ApiStorageVisibility.Private,
            GetDocumentsRoot(),
            storagePath,
            cancellationToken);

    private string GetDocumentsRoot()
    {
        var configuredRoot = _configuration["Storage:DocumentsRoot"];
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            configuredRoot = _configuration["DOCUMENT_STORAGE_ROOT"];
        }

        return string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(AppContext.BaseDirectory, "storage", "documents")
            : configuredRoot;
    }

    private async Task SaveFileAsync(string storagePath, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        await _objectStorage.SaveAsync(
            ApiStorageVisibility.Private,
            GetDocumentsRoot(),
            storagePath,
            stream,
            file.ContentType,
            cancellationToken);
    }

    private static string ToFolderName(CompanyDocumentType documentType)
    {
        return documentType switch
        {
            CompanyDocumentType.FiscalExistenceDeclaration => "dfe",
            CompanyDocumentType.BusinessRegistration => "registre-commerce",
            CompanyDocumentType.OwnerIdentity => "identite-responsable",
            CompanyDocumentType.AddressProof => "justificatif-adresse",
            _ => "autres-pieces"
        };
    }

    private static string ToProviderFolderName(ProviderDocumentType documentType)
    {
        return documentType switch
        {
            ProviderDocumentType.Photo => "photo",
            ProviderDocumentType.IdentityDocument => "piece-identite",
            ProviderDocumentType.Diploma => "diplome",
            _ => "documents"
        };
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Select(character => invalidChars.Contains(character) ? '-' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }

    private static bool IsAllowedFile(IFormFile file, string extension)
    {
        return AllowedExtensions.Contains(extension)
            && (AllowedContentTypes.Contains(file.ContentType)
                || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record StoredCompanyApplicationDocument(
    CompanyDocumentType DocumentType,
    string OriginalFileName,
    string StoragePath,
    string ContentType);

public sealed record StoredProviderDocument(
    ProviderDocumentType DocumentType,
    string OriginalFileName,
    string StoragePath,
    string ContentType);
