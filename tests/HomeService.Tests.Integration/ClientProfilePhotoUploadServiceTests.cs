using HomeService.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HomeService.Tests.Integration;

public sealed class ClientProfilePhotoUploadServiceTests : IDisposable
{
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), $"wele-profile-photo-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_AcceptsModernPhonePhotoAndStoresIt()
    {
        var service = CreateService();
        var bytes = new byte[6 * 1024 * 1024];
        Random.Shared.NextBytes(bytes);
        var file = CreateFile(bytes, "portrait.jpg", "image/jpeg");

        var relativePath = await service.SaveAsync(Guid.NewGuid(), file, CancellationToken.None);

        Assert.StartsWith("client-profiles/", relativePath);
        Assert.EndsWith(".jpg", relativePath);
        Assert.True(File.Exists(service.GetAbsolutePath(relativePath)));
        Assert.Equal(bytes.Length, new FileInfo(service.GetAbsolutePath(relativePath)).Length);
    }

    [Fact]
    public async Task SaveAsync_AcceptsAndroidPhotoWithoutFileExtension()
    {
        var service = CreateService();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var file = CreateFile(bytes, "image_picker_7319", "image/jpeg");

        var relativePath = await service.SaveAsync(Guid.NewGuid(), file, CancellationToken.None);

        Assert.EndsWith(".jpg", relativePath);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(service.GetAbsolutePath(relativePath)));
    }

    [Fact]
    public async Task SaveAsync_AcceptsAndroidMimeTypeWithParameters()
    {
        var service = CreateService();
        var file = CreateFile([1, 2, 3], "selected-photo", "image/webp; charset=binary");

        var relativePath = await service.SaveAsync(Guid.NewGuid(), file, CancellationToken.None);

        Assert.EndsWith(".webp", relativePath);
    }

    [Fact]
    public async Task SaveAsync_RejectsEmptyPhoto()
    {
        var service = CreateService();
        var file = CreateFile([], "portrait.jpg", "image/jpeg");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(Guid.NewGuid(), file, CancellationToken.None));

        Assert.Contains("12 Mo", exception.Message);
    }

    [Fact]
    public async Task SaveAsync_RejectsNonImageContentType()
    {
        var service = CreateService();
        var file = CreateFile([1, 2, 3], "portrait.jpg", "text/plain");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(Guid.NewGuid(), file, CancellationToken.None));

        Assert.Contains("Formats acceptes", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(storageRoot))
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private ClientProfilePhotoUploadService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = storageRoot })
            .Build();
        return new ClientProfilePhotoUploadService(configuration);
    }

    private static FormFile CreateFile(byte[] bytes, string fileName, string contentType)
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
