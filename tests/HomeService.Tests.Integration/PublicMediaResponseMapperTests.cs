using HomeService.Api;
using HomeService.Contracts.Services;

namespace HomeService.Tests.Integration;

public sealed class PublicMediaResponseMapperTests
{
    [Fact]
    public void Service_catalog_media_paths_are_returned_as_direct_cdn_urls()
    {
        var service = new ServiceSummaryResponse(
            Guid.NewGuid(),
            "Menage",
            null,
            "menage",
            "Active",
            true,
            1000,
            1500,
            "XOF",
            [
                new ServicePrestationSummaryResponse(
                    Guid.NewGuid(),
                    "Menage regulier",
                    null,
                    1,
                    1000,
                    1500,
                    "XOF",
                    true,
                    IllustrationUrl: "/catalog/prestations/menage-regulier.jpg")
            ],
            IconUrl: "/assets/services/menage.png");

        var mapped = PublicMediaResponseMapper.Map(new CdnStorage(), service);

        Assert.Equal("https://media.wele.africa/assets/services/menage.png?brand=20260815", mapped.IconUrl);
        Assert.Equal(
            "https://media.wele.africa/catalog/prestations/menage-regulier.jpg?brand=20260815",
            mapped.Prestations[0].IllustrationUrl);
    }

    [Fact]
    public void Existing_external_media_url_is_preserved()
    {
        const string externalUrl = "https://images.example.com/catalog/image.jpg";

        Assert.Equal(externalUrl, PublicMediaResponseMapper.Resolve(new CdnStorage(), externalUrl));
    }

    private sealed class CdnStorage : IApiObjectStorage
    {
        public bool UsesR2 => true;
        public string? GetPublicUrl(string objectKey) => $"https://media.wele.africa/{objectKey}";
        public string GetLocalAbsolutePath(string localRoot, string objectKey) => throw new NotSupportedException();
        public Task SaveAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, Stream content, string contentType, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream?> OpenReadAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteIfExistsAsync(ApiStorageVisibility visibility, string localRoot, string objectKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
