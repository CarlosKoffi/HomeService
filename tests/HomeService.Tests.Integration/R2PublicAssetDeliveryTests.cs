using HomeService.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HomeService.Tests.Integration;

public sealed class R2PublicAssetDeliveryTests
{
    [Fact]
    public async Task Direct_request_redirects_to_cdn()
    {
        var result = await InvokeAsync(QueryString.Empty);

        Assert.False(result.NextCalled);
        Assert.Equal(StatusCodes.Status302Found, result.Context.Response.StatusCode);
        Assert.Equal(
            "https://media.wele.africa/assets/services/menage.png",
            result.Context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Proxy_request_continues_to_packaged_static_asset()
    {
        var result = await InvokeAsync(new QueryString("?proxy=1"));

        Assert.True(result.NextCalled);
        Assert.NotEqual(StatusCodes.Status302Found, result.Context.Response.StatusCode);
        Assert.False(result.Context.Response.Headers.ContainsKey("Location"));
    }

    private static async Task<DeliveryResult> InvokeAsync(QueryString queryString)
    {
        var services = new ServiceCollection()
            .AddSingleton<IApiObjectStorage>(new CdnStorage())
            .BuildServiceProvider();
        var builder = new ApplicationBuilder(services);
        var nextCalled = false;
        builder.UseR2PublicAssetDelivery();
        builder.Run(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/assets/services/menage.png";
        context.Request.QueryString = queryString;

        await builder.Build()(context);
        return new DeliveryResult(context, nextCalled);
    }

    private sealed record DeliveryResult(DefaultHttpContext Context, bool NextCalled);

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
