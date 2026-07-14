using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CmsMediaAsset : AuditableEntity
{
    private readonly List<CmsMediaVariant> _variants = [];

    private CmsMediaAsset()
    {
    }

    public CmsMediaAsset(string fileName, string storagePath, string contentType, long sizeInBytes)
    {
        FileName = Normalize(fileName, nameof(fileName));
        StoragePath = Normalize(storagePath, nameof(storagePath));
        ContentType = Normalize(contentType, nameof(contentType));
        SizeInBytes = sizeInBytes < 0 ? throw new ArgumentOutOfRangeException(nameof(sizeInBytes)) : sizeInBytes;
    }

    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeInBytes { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string? AltText { get; private set; }
    public string? Checksum { get; private set; }
    public CmsMediaStatus Status { get; private set; } = CmsMediaStatus.Uploaded;
    public IReadOnlyCollection<CmsMediaVariant> Variants => _variants;

    public void MarkAvailable()
    {
        Status = CmsMediaStatus.Available;
        Touch();
    }

    private static string Normalize(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS media value is required.", parameterName)
            : value.Trim();
    }
}
