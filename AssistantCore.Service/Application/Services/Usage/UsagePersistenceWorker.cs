using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Usage;

public sealed class UsagePersistenceWorker(
    UsageConsumptionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<UsagePersistenceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var consumption in queue.ReadAllAsync(stoppingToken))
        {
            await PersistWithRetryAsync(consumption, stoppingToken);
        }
    }

    private async Task PersistWithRetryAsync(
        TokenConsumption consumption,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITokenConsumptionRepository>();
                await repository.TryRecordConsumptionAsync(consumption, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to persist token consumption for assistant message {AssistantMessageId}. Retrying.",
                    consumption.AssistantMessageId);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}
