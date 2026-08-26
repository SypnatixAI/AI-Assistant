using System.Data;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class Microsoft365PendingSynchronizationRepository(AssistantCoreDbContext dbContext)
    : IMicrosoft365PendingSynchronizationRepository
{
    public async Task<Microsoft365PendingSynchronization?> ClaimNextDriveAsync(
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var synchronization = await dbContext.Microsoft365Synchronizations
            .Where(candidate =>
                candidate.Microsoft365Source is Microsoft365Drive
                && (candidate.Status == Microsoft365SynchronizationStatus.Pending
                    || candidate.Status == Microsoft365SynchronizationStatus.TemporaryFailure)
                && (candidate.Type == Microsoft365SynchronizationType.Initial
                    || candidate.Type == Microsoft365SynchronizationType.Delta))
            .OrderBy(candidate => candidate.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (synchronization is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        synchronization.Status = Microsoft365SynchronizationStatus.Running;
        synchronization.StartedAt = startedAt;
        synchronization.AttemptCount++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Microsoft365PendingSynchronization(
            synchronization.Id,
            synchronization.Microsoft365SourceId,
            synchronization.Type);
    }
}
