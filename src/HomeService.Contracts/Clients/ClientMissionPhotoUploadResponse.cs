namespace HomeService.Contracts.Clients;

public sealed record ClientMissionPhotoUploadResponse(
    string OriginalFileName,
    string StoragePath,
    string ContentType,
    long FileSizeBytes,
    string? Caption);
