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
        var connectionId = options.Value.StartupConnectionId;
        if (!options.Value.RunStartupConnectionCheck || connectionId is null)
        {
            logger.LogInformation(
                "Microsoft 365 ingestion worker started and is waiting for a configured work source.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider
            .GetRequiredService<IMicrosoft365IngestionOrchestrator>();

        await orchestrator.ScheduleInitialSynchronizationAsync(
            connectionId.Value,
            stoppingToken);

        logger.LogInformation(
            "Microsoft 365 ingestion startup check accepted connection {ConnectionId}.",
            connectionId);
    }
}
