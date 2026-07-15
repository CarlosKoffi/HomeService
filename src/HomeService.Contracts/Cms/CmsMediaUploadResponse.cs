namespace HomeService.Contracts.Cms;

public sealed record CmsMediaUploadResponse(
    Guid MediaAssetId,
    string FileName,
    string Url,
    string ContentType,
    long SizeInBytes);
