using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ListSynchronizationService(
    IMicrosoft365ListSynchronizationRepository synchronizationRepository,
    IMicrosoft365SourceSynchronizationRepository sourceSynchronizationRepository,
    IMicrosoft365ListSchemaClient schemaClient,
    IMicrosoft365ListItemDeltaClient deltaClient,
    IMicrosoft365ListSchemaFingerprintGenerator fingerprintGenerator,
    IMicrosoft365ListItemWorkFactory workFactory,
    IOptions<Microsoft365Options> options,
    TimeProvider timeProvider) : IMicrosoft365ListSynchronizationService
{
    private const string FailedErrorCode = "MicrosoftGraphListSchemaLoadFailed";
    private const string CancelledErrorCode = "MicrosoftGraphListSchemaLoadCancelled";
    private const string InitialSynchronizationFailedErrorCode = "MicrosoftGraphInitialListSynchronizationFailed";
    private const string InitialSynchronizationCancelledErrorCode = "MicrosoftGraphInitialListSynchronizationCancelled";
    private const string DeltaSynchronizationFailedErrorCode = "MicrosoftGraphDeltaListSynchronizationFailed";
    private const string DeltaSynchronizationCancelledErrorCode = "MicrosoftGraphDeltaListSynchronizationCancelled";
    private const string DeltaCheckpointInvalidErrorCode = "MicrosoftGraphListDeltaCheckpointInvalid";

    public async Task<Microsoft365ListInitialSynchronizationResult> StartInitialSynchronizationAsync(
        Guid sourceId,
        Guid synchronizationId,
        CancellationToken cancellationToken = default)
    {
        var lease = await PrepareLeaseAsync(sourceId, cancellationToken);
        if (!lease.IsAcquired)
        {
            return new Microsoft365ListInitialSynchronizationResult(
                Microsoft365ListInitialSynchronizationStatus.AlreadyInProgress,
                ProcessWorkCount: 0,
                DeleteWorkCount: 0,
                PersistedWorkCount: 0,
                DeltaLink: null);
        }

        return await ExecuteUnderLeaseAsync(
            lease,
            synchronizationId,
            InitialSynchronizationFailedErrorCode,
            InitialSynchronizationCancelledErrorCode,
            async () =>
            {
                await UpdateSchemaAsync(lease.List, lease.LeaseId, cancellationToken);
                var synchronization = await SynchronizeItemsAsync(
                    lease,
                    synchronizationId,
                    deltaClient.GetInitialPagesAsync(
                        lease.List.Microsoft365Connection.TenantId!,
                        lease.List.SiteId,
                        lease.List.ListId,
                        cancellationToken),
                    cancellationToken);

                var result = new Microsoft365ListInitialSynchronizationResult(
                    Microsoft365ListInitialSynchronizationStatus.Completed,
                    synchronization.ProcessWorkCount,
                    synchronization.DeleteWorkCount,
                    synchronization.PersistedWorkCount,
                    synchronization.DeltaLink);
                return (result, synchronization.Counters);
            });
    }

    public async Task<Microsoft365ListDeltaSynchronizationResult> StartDeltaSynchronizationAsync(
        Guid sourceId,
        Guid synchronizationId,
        CancellationToken cancellationToken = default)
    {
        var lease = await PrepareLeaseAsync(sourceId, cancellationToken);
        if (!lease.IsAcquired)
        {
            return new Microsoft365ListDeltaSynchronizationResult(
                Microsoft365ListDeltaSynchronizationStatus.AlreadyInProgress,
                ProcessWorkCount: 0,
                DeleteWorkCount: 0,
                PersistedWorkCount: 0,
                DeltaLink: null);
        }

        return await ExecuteUnderLeaseAsync(
            lease,
            synchronizationId,
            DeltaSynchronizationFailedErrorCode,
            DeltaSynchronizationCancelledErrorCode,
            async () =>
            {
                ItemSynchronizationResult synchronization;
                if (lease.List.Status == Microsoft365SourceStatus.FullResyncRequired)
                {
                    await UpdateSchemaAsync(lease.List, lease.LeaseId, cancellationToken);
                    synchronization = await SynchronizeItemsAsync(
                        lease,
                        synchronizationId,
                        deltaClient.GetInitialPagesAsync(
                            lease.List.Microsoft365Connection.TenantId!,
                            lease.List.SiteId,
                            lease.List.ListId,
                            cancellationToken),
                        cancellationToken);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(lease.List.DeltaLink))
                    {
                        throw new InvalidOperationException(
                            "The Microsoft 365 list does not have a confirmed delta checkpoint.");
                    }

                    try
                    {
                        synchronization = await SynchronizeItemsAsync(
                            lease,
                            synchronizationId,
                            deltaClient.GetDeltaPagesAsync(
                                lease.List.Microsoft365Connection.TenantId!,
                                lease.List.DeltaLink,
                                cancellationToken),
                            cancellationToken);
                    }
                    catch (Microsoft365DeltaCheckpointInvalidException)
                    {
                        await MarkFullResyncRequiredAsync(
                            lease.List.Id,
                            lease.LeaseId,
                            DeltaCheckpointInvalidErrorCode,
                            cancellationToken);
                        await UpdateSchemaAsync(lease.List, lease.LeaseId, cancellationToken);
                        synchronization = await SynchronizeItemsAsync(
                            lease,
                            synchronizationId,
                            deltaClient.GetInitialPagesAsync(
                                lease.List.Microsoft365Connection.TenantId!,
                                lease.List.SiteId,
                                lease.List.ListId,
                                cancellationToken),
                            cancellationToken);
                    }
                }

                var result = new Microsoft365ListDeltaSynchronizationResult(
                    Microsoft365ListDeltaSynchronizationStatus.Completed,
                    synchronization.ProcessWorkCount,
                    synchronization.DeleteWorkCount,
                    synchronization.PersistedWorkCount,
                    synchronization.DeltaLink);
                return (result, synchronization.Counters);
            });
    }

    public async Task<Microsoft365ListSchemaSynchronizationResult> SynchronizeSchemaAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        var lease = await PrepareLeaseAsync(sourceId, cancellationToken);
        if (!lease.IsAcquired)
        {
            return new Microsoft365ListSchemaSynchronizationResult(
                Microsoft365ListSchemaSynchronizationStatus.AlreadyInProgress,
                lease.List.SchemaFingerprint,
                lease.List.RequiresItemReprocessing);
        }

        return await ExecuteUnderLeaseAsync(
            lease,
            synchronizationId: Guid.Empty,
            FailedErrorCode,
            CancelledErrorCode,
            async () =>
            {
                var result = await UpdateSchemaAsync(lease.List, lease.LeaseId, cancellationToken);
                return (result, Microsoft365SynchronizationCounters.Empty);
            },
            recordOutcome: false);
    }

    private async Task<ItemSynchronizationResult> SynchronizeItemsAsync(
        SynchronizationLease lease,
        Guid synchronizationId,
        IAsyncEnumerable<Microsoft365ListItemDeltaPage> pages,
        CancellationToken cancellationToken)
    {
        var processWorkCount = 0;
        var deleteWorkCount = 0;
        var persistedWorkCount = 0;
        var createdCount = 0;
        var modifiedCount = 0;
        string? finalDeltaLink = null;
        await foreach (var page in pages)
        {
            var pageCreatedAt = timeProvider.GetUtcNow();
            var works = page.Items
                .Select(item => workFactory.Create(lease.List, item, pageCreatedAt))
                .ToArray();
            processWorkCount += works.Count(work =>
                work.WorkType == Microsoft365ListItemWorkType.ProcessListItem);
            deleteWorkCount += works.Count(work =>
                work.WorkType == Microsoft365ListItemWorkType.DeleteListItem);
            foreach (var item in page.Items.Where(item => !item.IsDeleted))
            {
                if (IsCreated(item))
                {
                    createdCount++;
                }
                else
                {
                    modifiedCount++;
                }
            }

            persistedWorkCount += await synchronizationRepository.SaveWorkPageAsync(
                lease.List.Id,
                synchronizationId,
                lease.LeaseId,
                pageCreatedAt.AddMinutes(options.Value.SynchronizationLeaseMinutes),
                works,
                cancellationToken);
            finalDeltaLink = page.DeltaLink ?? finalDeltaLink;
        }

        if (string.IsNullOrWhiteSpace(finalDeltaLink))
        {
            throw new InvalidOperationException(
                "Microsoft Graph did not return a final delta link.");
        }

        var checkpointConfirmed = await sourceSynchronizationRepository.ConfirmCheckpointAsync(
            lease.List.Id,
            lease.LeaseId,
            finalDeltaLink,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (!checkpointConfirmed)
        {
            throw new InvalidOperationException(
                "The Microsoft 365 source synchronization lease was lost.");
        }

        return new ItemSynchronizationResult(
            processWorkCount,
            deleteWorkCount,
            persistedWorkCount,
            finalDeltaLink,
            new Microsoft365SynchronizationCounters(
                createdCount,
                modifiedCount,
                deleteWorkCount,
                IgnoredCount: 0,
                FailedCount: 0));
    }

    private async Task MarkFullResyncRequiredAsync(
        Guid sourceId,
        Guid leaseId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var statusMarked = await sourceSynchronizationRepository.MarkFullResyncRequiredAsync(
            sourceId,
            leaseId,
            errorCode,
            cancellationToken);
        if (!statusMarked)
        {
            throw new InvalidOperationException(
                "The Microsoft 365 source synchronization lease was lost.");
        }
    }

    private async Task<Microsoft365ListSchemaSynchronizationResult> UpdateSchemaAsync(
        Microsoft365List list,
        Guid leaseId,
        CancellationToken cancellationToken)
    {
        var columns = await schemaClient.GetColumnsAsync(
            list.Microsoft365Connection.TenantId!,
            list.SiteId,
            list.ListId,
            cancellationToken);
        var fingerprint = fingerprintGenerator.CreateFingerprint(columns);
        var schemaWasInitialized = list.SchemaFingerprint is not null;
        var schemaChanged = schemaWasInitialized
            && !string.Equals(list.SchemaFingerprint, fingerprint, StringComparison.Ordinal);
        var requiresItemReprocessing = list.RequiresItemReprocessing || schemaChanged;

        var schemaSaved = await synchronizationRepository.SaveSchemaAsync(
            list.Id,
            leaseId,
            fingerprint,
            requiresItemReprocessing,
            cancellationToken);
        if (!schemaSaved)
        {
            throw new InvalidOperationException(
                "The Microsoft 365 source synchronization lease was lost.");
        }

        var status = schemaChanged
            ? Microsoft365ListSchemaSynchronizationStatus.Changed
            : schemaWasInitialized
                ? Microsoft365ListSchemaSynchronizationStatus.Unchanged
                : Microsoft365ListSchemaSynchronizationStatus.Initialized;
        return new Microsoft365ListSchemaSynchronizationResult(
            status,
            fingerprint,
            requiresItemReprocessing);
    }

    private async Task<SynchronizationLease> PrepareLeaseAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var list = await synchronizationRepository.FindForSynchronizationAsync(
            sourceId,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 list was not found.");
        EnsureListCanBeSynchronized(list);

        var attemptedAt = timeProvider.GetUtcNow();
        var leaseId = Guid.NewGuid();
        var isAcquired = await sourceSynchronizationRepository.TryAcquireLeaseAsync(
            sourceId,
            leaseId,
            attemptedAt,
            attemptedAt.AddMinutes(options.Value.SynchronizationLeaseMinutes),
            cancellationToken);
        return new SynchronizationLease(list, leaseId, isAcquired);
    }

    private async Task<TResult> ExecuteUnderLeaseAsync<TResult>(
        SynchronizationLease lease,
        Guid synchronizationId,
        string failedErrorCode,
        string cancelledErrorCode,
        Func<Task<(TResult Result, Microsoft365SynchronizationCounters Counters)>> operation,
        bool recordOutcome = true)
    {
        string? releaseErrorCode = null;
        try
        {
            var outcome = await operation();
            if (recordOutcome)
            {
                await RecordSynchronizationOutcomeAsync(
                    lease.List.Id,
                    synchronizationId,
                    Microsoft365SynchronizationStatus.Succeeded,
                    outcome.Counters,
                    timeProvider.GetUtcNow(),
                    lastErrorCode: null,
                    CancellationToken.None);
            }

            return outcome.Result;
        }
        catch (OperationCanceledException)
        {
            releaseErrorCode = cancelledErrorCode;
            if (recordOutcome)
            {
                await RecordSynchronizationOutcomeAsync(
                    lease.List.Id,
                    synchronizationId,
                    Microsoft365SynchronizationStatus.Cancelled,
                    Microsoft365SynchronizationCounters.Empty with { FailedCount = 1 },
                    timeProvider.GetUtcNow(),
                    cancelledErrorCode,
                    CancellationToken.None);
            }

            throw;
        }
        catch (Microsoft365SourceAccessDeniedException)
        {
            releaseErrorCode = "MicrosoftGraphListAccessDenied";
            await MarkAccessErrorAsync(
                lease.List.Id,
                lease.LeaseId,
                releaseErrorCode,
                CancellationToken.None);
            if (recordOutcome)
            {
                await RecordSynchronizationOutcomeAsync(
                    lease.List.Id,
                    synchronizationId,
                    Microsoft365SynchronizationStatus.PermanentFailure,
                    Microsoft365SynchronizationCounters.Empty with { FailedCount = 1 },
                    timeProvider.GetUtcNow(),
                    releaseErrorCode,
                    CancellationToken.None);
            }

            throw;
        }
        catch (Microsoft365GraphTransientException)
        {
            releaseErrorCode = failedErrorCode;
            if (recordOutcome)
            {
                await RecordSynchronizationOutcomeAsync(
                    lease.List.Id,
                    synchronizationId,
                    Microsoft365SynchronizationStatus.TemporaryFailure,
                    Microsoft365SynchronizationCounters.Empty with { FailedCount = 1 },
                    timeProvider.GetUtcNow(),
                    failedErrorCode,
                    CancellationToken.None);
            }

            throw;
        }
        catch
        {
            releaseErrorCode = failedErrorCode;
            if (recordOutcome)
            {
                await RecordSynchronizationOutcomeAsync(
                    lease.List.Id,
                    synchronizationId,
                    Microsoft365SynchronizationStatus.PermanentFailure,
                    Microsoft365SynchronizationCounters.Empty with { FailedCount = 1 },
                    timeProvider.GetUtcNow(),
                    failedErrorCode,
                    CancellationToken.None);
            }

            throw;
        }
        finally
        {
            await sourceSynchronizationRepository.ReleaseLeaseAsync(
                lease.List.Id,
                lease.LeaseId,
                releaseErrorCode,
                CancellationToken.None);
        }
    }

    private async Task MarkAccessErrorAsync(
        Guid sourceId,
        Guid leaseId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var statusMarked = await sourceSynchronizationRepository.MarkAccessErrorAsync(
            sourceId,
            leaseId,
            errorCode,
            cancellationToken);
        if (!statusMarked)
        {
            throw new InvalidOperationException(
                "The Microsoft 365 source synchronization lease was lost.");
        }
    }

    private async Task RecordSynchronizationOutcomeAsync(
        Guid sourceId,
        Guid synchronizationId,
        Microsoft365SynchronizationStatus status,
        Microsoft365SynchronizationCounters counters,
        DateTimeOffset completedAt,
        string? lastErrorCode,
        CancellationToken cancellationToken)
    {
        var outcomeRecorded = await sourceSynchronizationRepository.RecordSynchronizationOutcomeAsync(
            sourceId,
            synchronizationId,
            status,
            counters,
            completedAt,
            lastErrorCode,
            cancellationToken);
        if (!outcomeRecorded)
        {
            throw new InvalidOperationException(
                "The Microsoft 365 synchronization could not be updated.");
        }
    }

    private static bool IsCreated(Microsoft365ListItemDelta item) =>
        item.CreatedDateTime is not null
        && item.LastModifiedDateTime is not null
        && item.CreatedDateTime == item.LastModifiedDateTime;

    private static void EnsureListCanBeSynchronized(
        Microsoft365List list)
    {
        var connection = list.Microsoft365Connection;
        if (!list.IsIndexed
            || list.Status is not Microsoft365SourceStatus.Enabled
                and not Microsoft365SourceStatus.FullResyncRequired
            || connection.Status != Microsoft365ConnectionStatus.Active
            || connection.OrganizationConnector.Status != RecordStatus.Active
            || !connection.OrganizationConnector.IsConfigured)
        {
            throw new InvalidOperationException(
                "Only an enabled Microsoft 365 list attached to an active connector can be synchronized.");
        }

        if (string.IsNullOrWhiteSpace(connection.TenantId))
        {
            throw new InvalidOperationException(
                "The Microsoft 365 connection tenant is missing.");
        }
    }

    private sealed record SynchronizationLease(
        Microsoft365List List,
        Guid LeaseId,
        bool IsAcquired);

    private sealed record ItemSynchronizationResult(
        int ProcessWorkCount,
        int DeleteWorkCount,
        int PersistedWorkCount,
        string DeltaLink,
        Microsoft365SynchronizationCounters Counters);
}
