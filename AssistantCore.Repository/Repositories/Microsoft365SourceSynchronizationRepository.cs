using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class Microsoft365SourceSynchronizationRepository(AssistantCoreDbContext dbContext)
    : IMicrosoft365SourceSynchronizationRepository
{
    public async Task<bool> TryAcquireLeaseAsync(
        Guid sourceId,
        Guid leaseId,
        DateTimeOffset attemptedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[Microsoft365Source]
            SET [SynchronizationLeaseId] = {leaseId},
                [SynchronizationLeaseExpiresAt] = {expiresAt},
                [LastSynchronizationAttemptAt] = {attemptedAt}
            WHERE [Id] = {sourceId}
              AND [Status] IN (N'Enabled', N'FullResyncRequired')
              AND [IsIndexed] = 1
              AND (
                  [SynchronizationLeaseId] IS NULL
                  OR [SynchronizationLeaseExpiresAt] <= {attemptedAt}
              );
            """, cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> ConfirmCheckpointAsync(
        Guid sourceId,
        Guid leaseId,
        string deltaLink,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[Microsoft365Source]
            SET [DeltaLink] = {deltaLink},
                [Status] = CASE
                    WHEN [Status] = N'FullResyncRequired' THEN N'Enabled'
                    ELSE [Status]
                END,
                [LastSuccessfulSynchronizationAt] = {completedAt},
                [LastErrorCode] = NULL
            WHERE [Id] = {sourceId}
              AND [SynchronizationLeaseId] = {leaseId};
            """, cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkFullResyncRequiredAsync(
        Guid sourceId,
        Guid leaseId,
        string lastErrorCode,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[Microsoft365Source]
            SET [Status] = N'FullResyncRequired',
                [LastErrorCode] = {lastErrorCode}
            WHERE [Id] = {sourceId}
              AND [SynchronizationLeaseId] = {leaseId};
            """, cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> MarkAccessErrorAsync(
        Guid sourceId,
        Guid leaseId,
        string lastErrorCode,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[Microsoft365Source]
            SET [Status] = N'Error',
                [LastErrorCode] = {lastErrorCode}
            WHERE [Id] = {sourceId}
              AND [SynchronizationLeaseId] = {leaseId};
            """, cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> RecordSynchronizationOutcomeAsync(
        Guid sourceId,
        Guid synchronizationId,
        Microsoft365SynchronizationStatus status,
        Microsoft365SynchronizationCounters counters,
        DateTimeOffset completedAt,
        string? lastErrorCode,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[Microsoft365Synchronization]
            SET [Status] = {status.ToString()},
                [CompletedAt] = {completedAt},
                [CreatedCount] = {counters.CreatedCount},
                [ModifiedCount] = {counters.ModifiedCount},
                [DeletedCount] = {counters.DeletedCount},
                [IgnoredCount] = {counters.IgnoredCount},
                [FailedCount] = {counters.FailedCount},
                [LastErrorCode] = {lastErrorCode}
            WHERE [Id] = {synchronizationId}
              AND [Microsoft365SourceId] = {sourceId};
            """, cancellationToken);

        return affectedRows == 1;
    }

    public Task ReleaseLeaseAsync(
        Guid sourceId,
        Guid leaseId,
        string? lastErrorCode,
        CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[Microsoft365Source]
            SET [SynchronizationLeaseId] = NULL,
                [SynchronizationLeaseExpiresAt] = NULL,
                [LastErrorCode] = {lastErrorCode}
            WHERE [Id] = {sourceId}
              AND [SynchronizationLeaseId] = {leaseId};
            """, cancellationToken);
}
