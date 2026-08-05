using HomeService.Api;
using HomeService.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HomeService.Tests.Integration;

public sealed class ProviderMobilePhotoUploadServiceTests : IDisposable
{
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), $"wele-provider-photo-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveMobileDocumentAsync_AcceptsAndroidPhotoWithoutExtension()
    {
        var service = CreateService();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02 };
        var file = CreateFile(bytes, "image_picker_1042", "image/jpeg");

        var stored = await service.SaveMobileDocumentAsync(
            Guid.NewGuid(),
            ProviderDocumentType.Photo,
            file,
            CancellationToken.None);

        Assert.EndsWith(".jpg", stored.StoragePath);
        Assert.EndsWith(".jpg", stored.OriginalFileName);
        Assert.Equal("image/jpeg", stored.ContentType);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(service.GetAbsolutePath(stored.StoragePath)));
    }

    [Fact]
    public async Task SaveMobileDocumentAsync_RejectsPdfUsedAsProfilePhoto()
    {
        var service = CreateService();
        var file = CreateFile([0x25, 0x50, 0x44, 0x46], "portrait.pdf", "application/pdf");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveMobileDocumentAsync(
            Guid.NewGuid(),
            ProviderDocumentType.Photo,
            file,
            CancellationToken.None));

        Assert.Contains("photo de profil", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SavePortfolioImageAsync_AcceptsExtensionlessMobileWebp()
    {
        var service = CreateService();
        var file = CreateFile([0x52, 0x49, 0x46, 0x46], "content", "image/webp; charset=binary");

        var stored = await service.SavePortfolioImageAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            file,
            CancellationToken.None);

        Assert.EndsWith(".webp", stored.StoragePath);
        Assert.Equal("image/webp", stored.ContentType);
        Assert.True(File.Exists(service.GetAbsolutePath(stored.StoragePath)));
    }

    public void Dispose()
    {
        if (Directory.Exists(storageRoot))
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private CompanyProviderUploadService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = storageRoot })
            .Build();
        return new CompanyProviderUploadService(configuration);
    }

    private static FormFile CreateFile(byte[] bytes, string fileName, string contentType)
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
