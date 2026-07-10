using HomeService.Domain.Enums;

namespace HomeService.Api;

public sealed class CompanyProviderUploadService(IConfiguration configuration)
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
    };

    private readonly string _rootPath = configuration["Storage:RootPath"]
        ?? configuration["STORAGE_ROOT_PATH"]
        ?? Path.Combine(AppContext.BaseDirectory, "storage");

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
                throw new InvalidOperationException("Chaque fichier employe doit faire moins de 10 Mo.");
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                throw new InvalidOperationException("Formats acceptes pour les employes: PDF, JPG, PNG ou WEBP.");
            }

            var extension = Path.GetExtension(file.FileName);
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.ToLowerInvariant();
            var relativePath = Path.Combine(
                "providers",
                companyId.ToString("D"),
                providerId.ToString("D"),
                $"{documentType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{safeExtension}");

            var absolutePath = GetAbsolutePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

            await using var stream = File.Create(absolutePath);
            await file.CopyToAsync(stream, cancellationToken);

            documents.Add(new StoredCompanyProviderDocument(
                documentType,
                file.FileName,
                relativePath.Replace('\\', '/'),
                file.ContentType));
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
            throw new InvalidOperationException("Chaque fichier employe doit faire moins de 10 Mo.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("Formats acceptes pour les employes: PDF, JPG, PNG ou WEBP.");
        }

        var extension = Path.GetExtension(file.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.ToLowerInvariant();
        var relativePath = Path.Combine(
            "providers",
            companyId.ToString("D"),
            providerId.ToString("D"),
            $"{documentType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{safeExtension}");

        var absolutePath = GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        return new StoredCompanyProviderDocument(
            documentType,
            file.FileName,
            relativePath.Replace('\\', '/'),
            file.ContentType);
    }

    public string GetAbsolutePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var root = Path.GetFullPath(_rootPath);

        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chemin de document invalide.");
        }

        return absolutePath;
    }

    private static IEnumerable<(string FieldName, ProviderDocumentType DocumentType)> GetDocumentFields()
    {
        yield return ("photo", ProviderDocumentType.Photo);
        yield return ("identityDocument", ProviderDocumentType.IdentityDocument);
        yield return ("diplomaDocument", ProviderDocumentType.Diploma);
    }
}

public sealed record StoredCompanyProviderDocument(
    ProviderDocumentType DocumentType,
    string OriginalFileName,
    string StoragePath,
    string ContentType);
