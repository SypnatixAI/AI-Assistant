using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistantCore.Service.Infrastructure.Health;

public sealed class SqlDatabaseHealthCheck(AssistantCoreDbContext database) : IHealthCheck
{
    public const string Name = "sql";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await database.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The SQL database is unavailable.");
    }
}
