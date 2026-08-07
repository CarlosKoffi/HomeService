using Microsoft.AspNetCore.StaticFiles;

namespace HomeService.Api;

public sealed class R2HistoricalAssetSeeder(
    IConfiguration configuration,
    IApiObjectStorage objectStorage,
    ILogger<R2HistoricalAssetSeeder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = IsEnabled(configuration);
        logger.LogWarning(
            "[STORAGE-DIAGNOSTIC] Historical asset migration starting. UsesR2={UsesR2}; Enabled={Enabled}.",
            objectStorage.UsesR2,
            enabled);

        if (!objectStorage.UsesR2 || !enabled)
        {
            logger.LogWarning(
                "[STORAGE-DIAGNOSTIC] Historical asset migration skipped. UsesR2={UsesR2}; Enabled={Enabled}.",
                objectStorage.UsesR2,
                enabled);
            return;
        }

        try
        {
            await MigrateAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "[STORAGE-DIAGNOSTIC] Historical asset migration failed. ExceptionType={ExceptionType}; Message={ExceptionMessage}. It will retry on the next API start.",
                exception.GetType().FullName,
                exception.Message);
        }
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var storageRoot = FirstConfigured(
                configuration["STORAGE_ROOT_PATH"],
                configuration["Storage:RootPath"])
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        var documentsRoot = FirstConfigured(
                configuration["DOCUMENT_STORAGE_ROOT"],
                configuration["Storage:DocumentsRoot"])
            ?? Path.Combine(storageRoot, "documents");

        var sources = new[]
        {
            new MigrationSource(ApiStorageVisibility.Public, storageRoot, "cms"),
            new MigrationSource(ApiStorageVisibility.Private, storageRoot, "client-profiles"),
            new MigrationSource(ApiStorageVisibility.Private, storageRoot, "client-missions"),
            new MigrationSource(ApiStorageVisibility.Private, storageRoot, "providers"),
            new MigrationSource(ApiStorageVisibility.Private, documentsRoot, "company-applications"),
            new MigrationSource(ApiStorageVisibility.Private, documentsRoot, "providers")
        };

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        var discovered = 0;
        var uploadedPublic = 0;
        var uploadedPrivate = 0;
        var alreadyPresent = 0;

        foreach (var source in sources)
        {
            var sourceRoot = Path.GetFullPath(source.LocalRoot);
            var folder = Path.GetFullPath(Path.Combine(sourceRoot, source.RelativeFolder));
            if (!Directory.Exists(folder))
            {
                logger.LogWarning(
                    "[STORAGE-DIAGNOSTIC] Historical asset folder is absent and was skipped. Visibility={Visibility}; Folder={Folder}.",
                    source.Visibility,
                    folder);
                continue;
            }

            var files = Directory.EnumerateFiles(
                    folder,
                    "*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.ReparsePoint,
                        IgnoreInaccessible = true
                    })
                .ToArray();
            discovered += files.Length;
            logger.LogWarning(
                "[STORAGE-DIAGNOSTIC] Historical asset folder discovered. Visibility={Visibility}; Folder={Folder}; FileCount={FileCount}.",
                source.Visibility,
                folder,
                files.Length);

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var absolutePath = Path.GetFullPath(filePath);
                if (!IsWithinRoot(sourceRoot, absolutePath))
                {
                    logger.LogWarning(
                        "[STORAGE-DIAGNOSTIC] Historical asset outside its expected root was skipped. File={FilePath}.",
                        absolutePath);
                    continue;
                }

                var objectKey = Path.GetRelativePath(sourceRoot, absolutePath).Replace('\\', '/');
                if (await objectStorage.ExistsAsync(
                    source.Visibility,
                    sourceRoot,
                    objectKey,
                    cancellationToken))
                {
                    alreadyPresent++;
                    continue;
                }

                await using var stream = new FileStream(
                    absolutePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var contentType = contentTypeProvider.TryGetContentType(absolutePath, out var resolvedContentType)
                    ? resolvedContentType
                    : "application/octet-stream";
                await objectStorage.SaveAsync(
                    source.Visibility,
                    sourceRoot,
                    objectKey,
                    stream,
                    contentType,
                    cancellationToken);

                if (source.Visibility == ApiStorageVisibility.Public)
                {
                    uploadedPublic++;
                }
                else
                {
                    uploadedPrivate++;
                }
            }
        }

        logger.LogWarning(
            "[STORAGE-DIAGNOSTIC] Historical asset migration completed. DiscoveredCount={DiscoveredCount}; UploadedPublicCount={UploadedPublicCount}; UploadedPrivateCount={UploadedPrivateCount}; ExistingCount={ExistingCount}.",
            discovered,
            uploadedPublic,
            uploadedPrivate,
            alreadyPresent);
    }

    private static bool IsEnabled(IConfiguration configuration)
    {
        var configured = FirstConfigured(
            configuration["R2_MIGRATE_LOCAL_ASSETS_ON_STARTUP"],
            configuration["R2:MigrateLocalAssetsOnStartup"],
            configuration["Storage:R2:MigrateLocalAssetsOnStartup"]);
        return !bool.TryParse(configured, out var enabled) || enabled;
    }

    private static bool IsWithinRoot(string root, string path)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstConfigured(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private sealed record MigrationSource(
        ApiStorageVisibility Visibility,
        string LocalRoot,
        string RelativeFolder);
}
