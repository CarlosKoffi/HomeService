using HomeService.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeService.Tests.Integration;

public sealed class R2HistoricalAssetSeederTests
{
    [Fact]
    public async Task Migration_copies_historical_public_and_private_assets_once()
    {
        var storageRoot = CreateTemporaryRoot();
        var documentsRoot = Path.Combine(storageRoot, "documents");
        WriteAsset(storageRoot, "cms/2026/08/banner.jpg", [1]);
        WriteAsset(storageRoot, "client-profiles/customer/photo.png", [2]);
        WriteAsset(storageRoot, "client-missions/pending/photo.jpg", [3]);
        WriteAsset(storageRoot, "providers/mobile/provider/photo.png", [4]);
        WriteAsset(documentsRoot, "company-applications/company/identite.pdf", [5]);
        WriteAsset(documentsRoot, "providers/company/provider/diplome.pdf", [6]);
        WriteAsset(storageRoot, "unrelated/ignored.txt", [7]);

        var objectStorage = new RecordingObjectStorage();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STORAGE_ROOT_PATH"] = storageRoot,
                ["DOCUMENT_STORAGE_ROOT"] = documentsRoot,
                ["R2_MIGRATE_LOCAL_ASSETS_ON_STARTUP"] = "true"
            })
            .Build();
        var seeder = new R2HistoricalAssetSeeder(
            configuration,
            objectStorage,
            NullLogger<R2HistoricalAssetSeeder>.Instance);

        await seeder.MigrateAsync(CancellationToken.None);
        await seeder.MigrateAsync(CancellationToken.None);

        Assert.Equal(["cms/2026/08/banner.jpg"], objectStorage.PublicKeys.Order());
        Assert.Equal(
            [
                "client-missions/pending/photo.jpg",
                "client-profiles/customer/photo.png",
                "company-applications/company/identite.pdf",
                "providers/company/provider/diplome.pdf",
                "providers/mobile/provider/photo.png"
            ],
            objectStorage.PrivateKeys.Order());
        Assert.DoesNotContain(objectStorage.PrivateKeys, key => key.StartsWith("documents/", StringComparison.Ordinal));
        Assert.DoesNotContain(objectStorage.AllKeys, key => key.Contains("unrelated", StringComparison.Ordinal));
        Assert.Equal(6, objectStorage.SaveCount);
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "homeservice-r2-history-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteAsset(string root, string relativePath, byte[] content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private sealed class RecordingObjectStorage : IApiObjectStorage
    {
        public HashSet<string> PublicKeys { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PrivateKeys { get; } = new(StringComparer.Ordinal);
        public IEnumerable<string> AllKeys => PublicKeys.Concat(PrivateKeys);
        public int SaveCount { get; private set; }
        public bool UsesR2 => true;

        public async Task SaveAsync(
            ApiStorageVisibility visibility,
            string localRoot,
            string objectKey,
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            using var sink = new MemoryStream();
            await content.CopyToAsync(sink, cancellationToken);
            Assert.NotEmpty(sink.ToArray());
            GetKeys(visibility).Add(objectKey);
            SaveCount++;
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
            CancellationToken cancellationToken) => Task.FromResult(GetKeys(visibility).Contains(objectKey));

        public Task DeleteIfExistsAsync(
            ApiStorageVisibility visibility,
            string localRoot,
            string objectKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetLocalAbsolutePath(string localRoot, string objectKey) => Path.Combine(localRoot, objectKey);
        public string? GetPublicUrl(string objectKey) => null;

        private HashSet<string> GetKeys(ApiStorageVisibility visibility) =>
            visibility == ApiStorageVisibility.Public ? PublicKeys : PrivateKeys;
    }
}
