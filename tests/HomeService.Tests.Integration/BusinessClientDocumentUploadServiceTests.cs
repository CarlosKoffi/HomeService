using HomeService.Api;
using HomeService.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HomeService.Tests.Integration;

public sealed class BusinessClientDocumentUploadServiceTests
{
    [Theory]
    [InlineData("registre.pdf", "application/pdf")]
    [InlineData("identite.jpg", "image/jpeg")]
    [InlineData("adresse.webp", "image/webp")]
    public async Task SaveAsync_WithAcceptedDocument_StoresItPrivately(
        string fileName,
        string contentType)
    {
        var storage = new RecordingStorage();
        var service = new BusinessClientDocumentUploadService(CreateConfiguration(), storage);
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var file = CreateFile(stream, fileName, contentType);

        var result = await service.SaveAsync(
            Guid.NewGuid(),
            BusinessClientDocumentType.BusinessRegistration,
            file,
            CancellationToken.None);

        Assert.Equal(fileName, result.OriginalFileName);
        Assert.Equal(ApiStorageVisibility.Private, storage.Visibility);
        Assert.Equal([1, 2, 3, 4], storage.Bytes);
        Assert.Contains("business-clients/", result.StoragePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_WithUnsupportedFile_IsRejectedBeforeStorage()
    {
        var storage = new RecordingStorage();
        var service = new BusinessClientDocumentUploadService(CreateConfiguration(), storage);
        await using var stream = new MemoryStream([1, 2, 3]);
        var file = CreateFile(stream, "piece.exe", "application/octet-stream");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            Guid.NewGuid(),
            BusinessClientDocumentType.BusinessRegistration,
            file,
            CancellationToken.None));

        Assert.Contains("PDF", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(storage.Bytes);
    }

    [Fact]
    public async Task SaveAsync_WithFileLargerThan25Mb_IsRejectedBeforeStorage()
    {
        var storage = new RecordingStorage();
        var service = new BusinessClientDocumentUploadService(CreateConfiguration(), storage);
        await using var stream = new MemoryStream([1]);
        var file = new FormFile(stream, 0, 25L * 1024 * 1024 + 1, "file", "registre.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(
            Guid.NewGuid(),
            BusinessClientDocumentType.BusinessRegistration,
            file,
            CancellationToken.None));

        Assert.Contains("25 Mo", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(storage.Bytes);
    }

    private static IFormFile CreateFile(Stream stream, string fileName, string contentType) =>
        new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>())
        .Build();

    private sealed class RecordingStorage : IApiObjectStorage
    {
        public ApiStorageVisibility? Visibility { get; private set; }
        public byte[]? Bytes { get; private set; }
        public bool UsesR2 => true;

        public async Task SaveAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
        {
            Visibility = visibility;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Bytes = buffer.ToArray();
        }

        public Task<Stream?> OpenReadAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(null);

        public Task<bool> ExistsAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(Bytes is not null);

        public Task DeleteIfExistsAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string GetLocalAbsolutePath(string localRoot, string objectKey) =>
            Path.Combine(localRoot, objectKey);

        public string? GetPublicUrl(string objectKey) => null;
    }
}
