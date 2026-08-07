using Microsoft.AspNetCore.StaticFiles;

namespace HomeService.Api;

public sealed class R2PublicAssetSeeder(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IApiObjectStorage objectStorage,
    ILogger<R2PublicAssetSeeder> logger) : BackgroundService
{
    private static readonly string[] PublicAssetFolders =
    [
        "assets/services",
        "catalog/prestations",
        "media/payment-providers"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seedEnabled = IsEnabled(configuration);
        logger.LogWarning(
            "[STORAGE-DIAGNOSTIC] Public asset seed starting. UsesR2={UsesR2}; SeedEnabled={SeedEnabled}; WebRootPath={WebRootPath}; WebRootExists={WebRootExists}.",
            objectStorage.UsesR2,
            seedEnabled,
            environment.WebRootPath ?? "<missing>",
            !string.IsNullOrWhiteSpace(environment.WebRootPath) && Directory.Exists(environment.WebRootPath));

        if (!objectStorage.UsesR2 || !seedEnabled)
        {
            logger.LogWarning(
                "[STORAGE-DIAGNOSTIC] Public asset seed skipped. UsesR2={UsesR2}; SeedEnabled={SeedEnabled}.",
                objectStorage.UsesR2,
                seedEnabled);
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
        var alreadyPresent = 0;
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
                if (await objectStorage.ExistsAsync(
                    ApiStorageVisibility.Public,
                    webRoot,
                    objectKey,
                    cancellationToken))
                {
                    alreadyPresent++;
                    continue;
                }

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
            "[STORAGE-DIAGNOSTIC] R2 public asset seed completed. DiscoveredCount={DiscoveredCount}; UploadedCount={UploadedCount}; ExistingCount={ExistingCount}.",
            discovered,
            uploaded,
            alreadyPresent);
    }

    private static bool IsEnabled(IConfiguration configuration)
    {
        var configured = configuration["Storage:R2:SeedPublicAssetsOnStartup"]
            ?? configuration["R2:SeedPublicAssetsOnStartup"]
            ?? configuration["R2_SEED_PUBLIC_ASSETS_ON_STARTUP"];
        return !bool.TryParse(configured, out var enabled) || enabled;
    }
}

public static class R2PublicAssetDeliveryExtensions
{
    private static readonly string[] PublicPrefixes =
    [
        "/assets/services/",
        "/catalog/prestations/",
        "/media/payment-providers/"
    ];

    public static IApplicationBuilder UseR2PublicAssetDelivery(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;
            if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && path is not null
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
