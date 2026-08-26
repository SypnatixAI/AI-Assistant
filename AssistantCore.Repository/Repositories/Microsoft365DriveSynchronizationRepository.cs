using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class Microsoft365DriveSynchronizationRepository(AssistantCoreDbContext dbContext)
    : IMicrosoft365DriveSynchronizationRepository
{
    public Task<Microsoft365Drive?> FindForSynchronizationAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default) =>
        dbContext.Microsoft365Drives
            .AsNoTracking()
            .Include(drive => drive.Microsoft365Connection)
                .ThenInclude(connection => connection.OrganizationConnector)
            .SingleOrDefaultAsync(drive => drive.Id == sourceId, cancellationToken);

    public async Task<int> SaveWorkPageAsync(
        Guid sourceId,
        Guid synchronizationId,
        Guid leaseId,
        DateTimeOffset leaseExpiresAt,
        IReadOnlyCollection<Microsoft365DocumentWorkData> works,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var renewedLeaseRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [dbo].[Microsoft365Source]
            SET [SynchronizationLeaseExpiresAt] = {leaseExpiresAt}
            WHERE [Id] = {sourceId}
              AND [SynchronizationLeaseId] = {leaseId};
            """, cancellationToken);
        if (renewedLeaseRows != 1)
        {
            throw new InvalidOperationException(
                "The Microsoft 365 source synchronization lease was lost.");
        }

        var synchronizationExists = await dbContext.Microsoft365Synchronizations
            .AsNoTracking()
            .AnyAsync(synchronization =>
                synchronization.Id == synchronizationId
                && synchronization.Microsoft365SourceId == sourceId,
                cancellationToken);
        if (!synchronizationExists)
        {
            throw new InvalidOperationException(
                "The Microsoft 365 synchronization does not belong to the requested source.");
        }

        var distinctWorks = works
            .GroupBy(work => work.DeduplicationKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var deduplicationKeys = distinctWorks.Select(work => work.DeduplicationKey).ToArray();
        var existingKeys = deduplicationKeys.Length == 0
            ? Array.Empty<string>()
            : await dbContext.Microsoft365DocumentWorks
                .AsNoTracking()
                .Where(work => deduplicationKeys.Contains(work.DeduplicationKey))
                .Select(work => work.DeduplicationKey)
                .ToArrayAsync(cancellationToken);
        var existingKeySet = existingKeys.ToHashSet(StringComparer.Ordinal);
        var newWorks = distinctWorks
            .Where(work => !existingKeySet.Contains(work.DeduplicationKey))
            .Select(work => new Microsoft365DocumentWork
            {
                Id = Guid.NewGuid(),
                OrganizationId = work.OrganizationId,
                Microsoft365SourceId = sourceId,
                Microsoft365SynchronizationId = synchronizationId,
                WorkType = work.WorkType,
                SiteId = work.SiteId,
                DriveId = work.DriveId,
                DriveItemId = work.DriveItemId,
                Name = work.Name,
                ETag = work.ETag,
                CreatedDateTime = work.CreatedDateTime,
                LastModifiedDateTime = work.LastModifiedDateTime,
                WebUrl = work.WebUrl,
                Size = work.Size,
                MimeType = work.MimeType,
                DeduplicationKey = work.DeduplicationKey,
                Status = Microsoft365DocumentWorkStatus.Pending,
                CreatedAt = work.CreatedAt
            })
            .ToArray();

        dbContext.Microsoft365DocumentWorks.AddRange(newWorks);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return newWorks.Length;
    }
}
