using HomeService.Application.Missions;
using HomeService.Application.Clients;

namespace HomeService.Api;

public sealed class MissionDispatchAutomationHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MissionDispatchAutomationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);
    private const int DefaultBatchSize = 50;
    private bool startupRecoveryPending = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["MISSION_DISPATCH_AUTOMATION_ENABLED"], "false", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Mission dispatch automation is disabled.");
            return;
        }

        var interval = GetInterval();
        using var timer = new PeriodicTimer(interval);

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
            var dispatchService = scope.ServiceProvider.GetRequiredService<MissionDispatchService>();
            var assignmentExpirationService = scope.ServiceProvider.GetRequiredService<ProviderAssignmentExpirationService>();
            var clientConfirmationService = scope.ServiceProvider.GetRequiredService<ClientMissionConfirmationService>();
            var clientCompletionService = scope.ServiceProvider.GetRequiredService<ClientMissionCompletionValidationService>();
            var now = DateTimeOffset.UtcNow;
            var recoveredRecentCount = 0;
            if (startupRecoveryPending)
            {
                var yesterday = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-1), TimeSpan.Zero);
                recoveredRecentCount = (await assignmentExpirationService.RecoverRecentStalledAssignmentsAsync(
                    yesterday,
                    now,
                    GetBatchSize(),
                    stoppingToken)).ExpiredAssignmentCount;
                startupRecoveryPending = false;
            }

            var assignmentResult = await assignmentExpirationService.ExpireDueAssignmentsAsync(
                now,
                GetBatchSize(),
                stoppingToken);
            var recoveredMissionCount = await dispatchService.DispatchUnroutedMissionsAsync(
                GetBatchSize(),
                stoppingToken);
            var result = await dispatchService.ExpireAndReissueDueOffersAsync(
                now,
                GetBatchSize(),
                stoppingToken);
            var autoConfirmedCount = await clientConfirmationService.AutoConfirmExpiredAsync(
                now,
                GetBatchSize(),
                stoppingToken);
            var autoValidatedCount = await clientCompletionService.AutoValidateExpiredAsync(
                now,
                GetBatchSize(),
                stoppingToken);

            if (recoveredRecentCount > 0
                || recoveredMissionCount > 0
                || result.MissionCount > 0
                || assignmentResult.ExpiredAssignmentCount > 0
                || autoConfirmedCount > 0
                || autoValidatedCount > 0)
            {
                logger.LogInformation(
                    "Mission dispatch automation repaired {RecoveredRecentCount} recent stalled missions, recovered {RecoveredMissionCount} unrouted missions, processed {MissionCount} missions, expired {ExpiredCount} offers, created {CreatedCount} offers, expired {AssignmentCount} provider assignments, auto-confirmed {AutoConfirmedCount} client payments and auto-validated {AutoValidatedCount} completed missions.",
                    recoveredRecentCount,
                    recoveredMissionCount,
                    result.MissionCount,
                    result.ExpiredOfferCount,
                    result.CreatedOfferCount,
                    assignmentResult.ExpiredAssignmentCount,
                    autoConfirmedCount,
                    autoValidatedCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Mission dispatch automation failed.");
        }
    }

    private TimeSpan GetInterval()
    {
        return int.TryParse(configuration["MISSION_DISPATCH_AUTOMATION_INTERVAL_SECONDS"], out var seconds)
            ? TimeSpan.FromSeconds(Math.Clamp(seconds, 10, 3600))
            : DefaultInterval;
    }

    private int GetBatchSize()
    {
        return int.TryParse(configuration["MISSION_DISPATCH_AUTOMATION_BATCH_SIZE"], out var batchSize)
            ? Math.Clamp(batchSize, 1, 200)
            : DefaultBatchSize;
    }
}
