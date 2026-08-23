using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantCore.Ingestion.Worker;

public sealed class Microsoft365IngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<Microsoft365WorkerOptions> options,
    ILogger<Microsoft365IngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.RunStartupConnectionCheck
            && options.Value.StartupConnectionId is { } connectionId)
        {
            await using var startupScope = scopeFactory.CreateAsyncScope();
            var orchestrator = startupScope.ServiceProvider
                .GetRequiredService<IMicrosoft365IngestionOrchestrator>();
            await orchestrator.ScheduleInitialSynchronizationAsync(connectionId, stoppingToken);
            logger.LogInformation(
                "Microsoft 365 ingestion startup check accepted connection {ConnectionId}.",
                connectionId);
        }

        logger.LogInformation(
            "Microsoft 365 ingestion worker started subscription maintenance and reconciliation.");
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.MaintenanceIntervalSeconds));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var maintenanceService = scope.ServiceProvider
                    .GetRequiredService<IMicrosoft365SubscriptionMaintenanceService>();
                await maintenanceService.RunMaintenanceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Microsoft 365 subscription maintenance cycle failed.");
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reconciliationService = scope.ServiceProvider
                    .GetRequiredService<IMicrosoft365ReconciliationService>();
                await reconciliationService.RunReconciliationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Microsoft 365 reconciliation cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
