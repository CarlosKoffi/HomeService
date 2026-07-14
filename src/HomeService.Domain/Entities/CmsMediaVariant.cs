using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CmsMediaVariant : AuditableEntity
{
    private CmsMediaVariant()
    {
    }

    public CmsMediaVariant(Guid mediaAssetId, string variantKey, string storagePath, string contentType, long sizeInBytes)
    {
        MediaAssetId = mediaAssetId;
        VariantKey = Normalize(variantKey, nameof(variantKey));
        StoragePath = Normalize(storagePath, nameof(storagePath));
        ContentType = Normalize(contentType, nameof(contentType));
        SizeInBytes = sizeInBytes < 0 ? throw new ArgumentOutOfRangeException(nameof(sizeInBytes)) : sizeInBytes;
    }

    public Guid MediaAssetId { get; private set; }
    public CmsMediaAsset? MediaAsset { get; private set; }
    public string VariantKey { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeInBytes { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }

    private static string Normalize(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS media variant value is required.", parameterName)
            : value.Trim();
    }
}
