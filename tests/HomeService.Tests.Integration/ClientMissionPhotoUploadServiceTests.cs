using HomeService.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HomeService.Tests.Integration;

public sealed class ClientMissionPhotoUploadServiceTests
{
    [Fact]
    public async Task SaveAsync_WhenImageIsValid_StoresPhotoAndReturnsMissionReference()
    {
        var root = CreateStorageRoot();
        var service = new ClientMissionPhotoUploadService(CreateConfiguration(root));
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var file = new FormFile(stream, 0, stream.Length, "photo", "evier.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await service.SaveAsync(file, "Fuite sous evier", CancellationToken.None);

        Assert.Equal("evier.jpg", result.OriginalFileName);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal("Fuite sous evier", result.Caption);
        Assert.StartsWith("client-missions/pending/", result.StoragePath);
        Assert.True(File.Exists(service.GetAbsolutePath(result.StoragePath)));
    }

    [Fact]
    public async Task SaveAsync_WhenFileIsNotImage_IsRejected()
    {
        var root = CreateStorageRoot();
        var service = new ClientMissionPhotoUploadService(CreateConfiguration(root));
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var file = new FormFile(stream, 0, stream.Length, "photo", "devis.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync(file, null, CancellationToken.None));

        Assert.Contains("Formats photos", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration CreateConfiguration(string root)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:RootPath"] = root
            })
            .Build();
    }

    private static string CreateStorageRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "homeservice-client-photo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
