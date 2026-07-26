using HomeService.Application.Notifications;

namespace HomeService.Api;

public sealed class MobilePushNotificationHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MobilePushNotificationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);
    private const int DefaultBatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.Equals(configuration["FIREBASE_NOTIFICATIONS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Firebase mobile push notifications are disabled.");
            return;
        }

        using var timer = new PeriodicTimer(GetInterval());
        await ProcessAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessAsync(stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<MobilePushOutboxDispatcherService>();
            var result = await dispatcher.DispatchPendingAsync(
                DateTimeOffset.UtcNow,
                GetBatchSize(),
                stoppingToken);

            if (result.ProcessedCount > 0)
            {
                logger.LogInformation(
                    "Mobile push notifications processed {ProcessedCount}: {SentCount} sent, {FailedCount} failed.",
                    result.ProcessedCount,
                    result.SentCount,
                    result.FailedCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Mobile push notification processing failed.");
        }
    }

    private TimeSpan GetInterval()
    {
        return int.TryParse(configuration["FIREBASE_NOTIFICATIONS_INTERVAL_SECONDS"], out var seconds)
            ? TimeSpan.FromSeconds(Math.Clamp(seconds, 10, 3600))
            : DefaultInterval;
    }

    private int GetBatchSize()
    {
        return int.TryParse(configuration["FIREBASE_NOTIFICATIONS_BATCH_SIZE"], out var batchSize)
            ? Math.Clamp(batchSize, 1, 200)
            : DefaultBatchSize;
    }
}
