using HomeService.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeService.Tests.Integration;

public sealed class R2PublicAssetSeederTests
{
    [Fact]
    public async Task Seeder_uploads_expected_public_assets_once()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "homeservice-r2-seed-tests", Guid.NewGuid().ToString("N"));
        WriteAsset(webRoot, "assets/services/plomberie.png", [1, 2, 3]);
        WriteAsset(webRoot, "catalog/prestations/reparer-fuite.jpg", [4, 5, 6]);
        WriteAsset(webRoot, "media/payment-providers/wave.svg", [7, 8, 9]);
        WriteAsset(webRoot, "unrelated/ignored.png", [10]);

        var storage = new RecordingObjectStorage();
        var seeder = new R2PublicAssetSeeder(
            new TestWebHostEnvironment(webRoot),
            new ConfigurationBuilder().Build(),
            storage,
            NullLogger<R2PublicAssetSeeder>.Instance);

        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(3, storage.SavedKeys.Count);
        Assert.Contains("assets/services/plomberie.png", storage.SavedKeys);
        Assert.Contains("catalog/prestations/reparer-fuite.jpg", storage.SavedKeys);
        Assert.Contains("media/payment-providers/wave.svg", storage.SavedKeys);
        Assert.DoesNotContain("unrelated/ignored.png", storage.SavedKeys);
    }

    private static void WriteAsset(string webRoot, string relativePath, byte[] content)
    {
        var path = Path.Combine(webRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private sealed class RecordingObjectStorage : IApiObjectStorage
    {
        public HashSet<string> SavedKeys { get; } = new(StringComparer.Ordinal);
        public bool UsesR2 => true;

        public async Task SaveAsync(
            ApiStorageVisibility visibility,
            string localRoot,
            string objectKey,
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            Assert.Equal(ApiStorageVisibility.Public, visibility);
            using var sink = new MemoryStream();
            await content.CopyToAsync(sink, cancellationToken);
            Assert.NotEmpty(sink.ToArray());
            SavedKeys.Add(objectKey);
        }

        public Task<Stream?> OpenReadAsync(
            ApiStorageVisibility visibility,
            string localRoot,
            string objectKey,
            CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);

        public Task<bool> ExistsAsync(
            ApiStorageVisibility visibility,
            string localRoot,
            string objectKey,
            CancellationToken cancellationToken) => Task.FromResult(SavedKeys.Contains(objectKey));

        public Task DeleteIfExistsAsync(
            ApiStorageVisibility visibility,
            string localRoot,
            string objectKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetLocalAbsolutePath(string localRoot, string objectKey) => Path.Combine(localRoot, objectKey);
        public string? GetPublicUrl(string objectKey) => null;
    }

    private sealed class TestWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "HomeService.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(webRootPath);
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = webRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(webRootPath);
    }
}
