using AssistantCore.Repository.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssistantCore.Service.Application.Services.Usage;

public sealed class UsageQuotaCacheInitializer(
    IUsageQuotaCache quotaCache,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var (periodStartsAt, periodEndsAt) = UsagePeriodCalculator.ComputeMonthlyPeriod(
            timeProvider.GetUtcNow());

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITokenConsumptionRepository>();
        var usageByOrganization = await repository.SumTokensByOrganizationForPeriodAsync(
            periodStartsAt,
            periodEndsAt,
            cancellationToken);

        quotaCache.Initialize(
            periodStartsAt,
            periodEndsAt,
            usageByOrganization);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
