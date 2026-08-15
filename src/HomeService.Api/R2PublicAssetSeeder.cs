using Microsoft.AspNetCore.StaticFiles;

namespace HomeService.Api;

public sealed class R2PublicAssetSeeder(
    IWebHostEnvironment environment,
    IApiObjectStorage objectStorage,
    ILogger<R2PublicAssetSeeder> logger) : BackgroundService
{
    private static readonly string[] PublicAssetFolders =
    [
        "assets/services",
        "catalog/prestations",
        "media/payment-providers",
        "website/client",
        "website/provider",
        "website/company"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogWarning(
            "[STORAGE-DIAGNOSTIC] Public asset synchronization starting. UsesR2={UsesR2}; WebRootPath={WebRootPath}; WebRootExists={WebRootExists}.",
            objectStorage.UsesR2,
            environment.WebRootPath ?? "<missing>",
            !string.IsNullOrWhiteSpace(environment.WebRootPath) && Directory.Exists(environment.WebRootPath));

        if (!objectStorage.UsesR2)
        {
            logger.LogWarning(
                "[STORAGE-DIAGNOSTIC] Public asset synchronization skipped because R2 is disabled.");
            return;
        }

        try
        {
            await SeedAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "[STORAGE-DIAGNOSTIC] R2 public asset seed failed. ExceptionType={ExceptionType}; Message={ExceptionMessage}. It will retry on the next API start.",
                exception.GetType().FullName,
                exception.Message);
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var webRoot = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot) || !Directory.Exists(webRoot))
        {
            logger.LogWarning("The API web root is unavailable; no public asset was seeded to R2.");
            return;
        }

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        var uploaded = 0;
        var discovered = 0;

        foreach (var relativeFolder in PublicAssetFolders)
        {
            var folder = Path.Combine(webRoot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folder))
            {
                logger.LogWarning(
                    "[STORAGE-DIAGNOSTIC] Public asset folder is missing. RelativeFolder={RelativeFolder}; AbsoluteFolder={AssetFolder}.",
                    relativeFolder,
                    folder);
                continue;
            }

            var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToArray();
            discovered += files.Length;
            logger.LogWarning(
                "[STORAGE-DIAGNOSTIC] Public asset folder discovered. RelativeFolder={RelativeFolder}; FileCount={FileCount}.",
                relativeFolder,
                files.Length);

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var objectKey = Path.GetRelativePath(webRoot, filePath).Replace('\\', '/');
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var contentType = contentTypeProvider.TryGetContentType(filePath, out var resolvedContentType)
                    ? resolvedContentType
                    : "application/octet-stream";

                await objectStorage.SaveAsync(
                    ApiStorageVisibility.Public,
                    webRoot,
                    objectKey,
                    stream,
                    contentType,
                    cancellationToken);
                uploaded++;
            }
        }

        logger.LogWarning(
            "[STORAGE-DIAGNOSTIC] R2 public asset synchronization completed. DiscoveredCount={DiscoveredCount}; UploadedCount={UploadedCount}.",
            discovered,
            uploaded);
    }
}

public static class R2PublicAssetDeliveryExtensions
{
    private static readonly string[] PublicPrefixes =
    [
        "/assets/services/",
        "/catalog/prestations/",
        "/media/payment-providers/",
        "/website/client/",
        "/website/provider/",
        "/website/company/"
    ];

    public static IApplicationBuilder UseR2PublicAssetDelivery(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;
            if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && path is not null
                && !string.Equals(context.Request.Query["proxy"], "1", StringComparison.Ordinal)
                && PublicPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                var storage = context.RequestServices.GetRequiredService<IApiObjectStorage>();
                var publicUrl = storage.GetPublicUrl(path.TrimStart('/'));
                if (!string.IsNullOrWhiteSpace(publicUrl))
                {
                    context.Response.Redirect(publicUrl, permanent: false);
                    return;
                }
            }

            await next(context);
        });
    }
}
