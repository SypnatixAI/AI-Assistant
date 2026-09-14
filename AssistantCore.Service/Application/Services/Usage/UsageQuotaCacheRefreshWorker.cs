using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Usage;

public sealed class UsageQuotaCacheRefreshWorker(
    IUsageQuotaCache quotaCache,
    IServiceScopeFactory scopeFactory,
    IOptions<UsageOptions> options,
    TimeProvider timeProvider,
    ILogger<UsageQuotaCacheRefreshWorker> logger) : BackgroundService
{
    private readonly TimeSpan refreshInterval = TimeSpan.FromSeconds(
        options.Value.CacheRefreshIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(refreshInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Unable to refresh the usage quota cache. The current in-memory snapshot is preserved.");
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var (periodStartsAt, periodEndsAt) = UsagePeriodCalculator.ComputeMonthlyPeriod(
            timeProvider.GetUtcNow());

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITokenConsumptionRepository>();
        var usageByOrganization = await repository.SumTokensByOrganizationForPeriodAsync(
            periodStartsAt,
            periodEndsAt,
            cancellationToken);

        quotaCache.Merge(
            periodStartsAt,
            periodEndsAt,
            usageByOrganization);
    }
}
