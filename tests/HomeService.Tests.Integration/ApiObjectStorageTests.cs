using HomeService.Api;
using Microsoft.Extensions.Configuration;

namespace HomeService.Tests.Integration;

public sealed class ApiObjectStorageTests
{
    [Fact]
    public async Task Local_storage_supports_roundtrip_and_delete()
    {
        var root = CreateTemporaryRoot();
        using var storage = new ApiObjectStorage(CreateConfiguration());
        var expected = "contenu image"u8.ToArray();

        await using (var source = new MemoryStream(expected))
        {
            await storage.SaveAsync(
                ApiStorageVisibility.Public,
                root,
                "cms/2026/08/image test.jpg",
                source,
                "image/jpeg",
                CancellationToken.None);
        }

        await using (var stored = await storage.OpenReadAsync(
            ApiStorageVisibility.Public,
            root,
            "cms/2026/08/image test.jpg",
            CancellationToken.None))
        {
            Assert.NotNull(stored);
            using var buffer = new MemoryStream();
            await stored!.CopyToAsync(buffer);
            Assert.Equal(expected, buffer.ToArray());
        }

        await storage.DeleteIfExistsAsync(
            ApiStorageVisibility.Public,
            root,
            "cms/2026/08/image test.jpg");

        Assert.False(await storage.ExistsAsync(
            ApiStorageVisibility.Public,
            root,
            "cms/2026/08/image test.jpg",
            CancellationToken.None));
        Assert.Null(await storage.OpenReadAsync(
            ApiStorageVisibility.Public,
            root,
            "cms/2026/08/image test.jpg",
            CancellationToken.None));
    }

    [Fact]
    public void Local_storage_rejects_path_traversal()
    {
        using var storage = new ApiObjectStorage(CreateConfiguration());
        var root = CreateTemporaryRoot();

        Assert.Throws<InvalidOperationException>(() =>
            storage.GetLocalAbsolutePath(root, "../secret.txt"));
    }

    [Fact]
    public void R2_configuration_fails_fast_when_a_secret_is_missing()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "R2",
            ["R2:AccountId"] = "account",
            ["R2:AccessKeyId"] = "access",
            ["R2:PublicBucket"] = "public",
            ["R2:PrivateBucket"] = "private"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new ApiObjectStorage(configuration));

        Assert.Contains("SecretAccessKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void R2_public_url_encodes_each_object_key_segment()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "R2",
            ["R2:AccountId"] = "account",
            ["R2:AccessKeyId"] = "access",
            ["R2:SecretAccessKey"] = "secret",
            ["R2:PublicBucket"] = "public",
            ["R2:PrivateBucket"] = "private",
            ["R2:PublicBaseUrl"] = "https://media.wele.africa/",
            ["R2:PublicAssetVersion"] = "mobile-optimized",
            ["R2:PublicDirectDeliveryEnabled"] = "true"
        });
        using var storage = new ApiObjectStorage(configuration);

        Assert.Equal(
            "https://media.wele.africa/cms/mon%20image.jpg?v=mobile-optimized",
            storage.GetPublicUrl("cms/mon image.jpg"));
    }

    [Fact]
    public void Coolify_environment_variables_override_local_appsettings_defaults()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "Local",
            ["Storage:R2:PublicBucket"] = "default-public",
            ["Storage:R2:PrivateBucket"] = "default-private",
            ["Storage:R2:PublicBaseUrl"] = "",
            ["Storage:R2:PublicDirectDeliveryEnabled"] = "false",
            ["STORAGE_PROVIDER"] = "R2",
            ["R2_ACCOUNT_ID"] = "account",
            ["R2_ACCESS_KEY_ID"] = "access",
            ["R2_SECRET_ACCESS_KEY"] = "secret",
            ["R2_PUBLIC_BUCKET"] = "wele-public-media-prod",
            ["R2_PRIVATE_BUCKET"] = "wele-private-media-prod",
            ["R2_PUBLIC_BASE_URL"] = "https://media.wele.africa",
            ["R2_PUBLIC_DIRECT_DELIVERY_ENABLED"] = "true"
        });

        using var storage = new ApiObjectStorage(configuration);

        Assert.True(storage.UsesR2);
        Assert.Equal(
            "https://media.wele.africa/assets/services/menage.png",
            storage.GetPublicUrl("assets/services/menage.png"));
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "homeservice-r2-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
