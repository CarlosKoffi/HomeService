using HomeService.Domain.Enums;

namespace HomeService.Api;

public sealed class CompanyApplicationUploadService(IConfiguration configuration)
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/octet-stream"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png"
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
                throw new InvalidOperationException($"Le fichier {file.FileName} depasse la limite de 10 Mo.");
            }

            var originalFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalFileName);
            if (!AllowedContentTypes.Contains(file.ContentType) || !AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"Le format du fichier {file.FileName} n'est pas accepte.");
            }

            var root = GetDocumentsRoot();
            var relativeDirectory = Path.Combine(
                "company-applications",
                DateTime.UtcNow.ToString("yyyy"),
                DateTime.UtcNow.ToString("MM"),
                companyApplicationId.ToString("N"),
                ToFolderName(documentType));
            var absoluteDirectory = Path.Combine(root, relativeDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{SanitizeFileName(originalFileName)}";
            var absolutePath = Path.Combine(absoluteDirectory, safeFileName);
            await using var stream = File.Create(absolutePath);
            await file.CopyToAsync(stream, cancellationToken);

            var relativePath = Path.Combine(relativeDirectory, safeFileName).Replace('\\', '/');
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
            throw new InvalidOperationException($"Le fichier {file.FileName} depasse la limite de 10 Mo.");
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedContentTypes.Contains(file.ContentType) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Le format du fichier {file.FileName} n'est pas accepte.");
        }

        var root = GetDocumentsRoot();
        var relativeDirectory = Path.Combine(
            "providers",
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            providerId.ToString("N"),
            ToProviderFolderName(documentType));
        var absoluteDirectory = Path.Combine(root, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{SanitizeFileName(originalFileName)}";
        var absolutePath = Path.Combine(absoluteDirectory, safeFileName);
        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        var relativePath = Path.Combine(relativeDirectory, safeFileName).Replace('\\', '/');
        return new StoredProviderDocument(documentType, originalFileName, relativePath, file.ContentType);
    }

    public string GetAbsolutePath(string storagePath)
    {
        var root = Path.GetFullPath(GetDocumentsRoot());
        var normalizedPath = storagePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(root, normalizedPath));

        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chemin de document invalide.");
        }

        return absolutePath;
    }

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
