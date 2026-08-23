using System.Runtime.CompilerServices;
using System.Text.Json;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ListSynchronizationServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_ThreeInitialDeltaPages_When_StartInitialSynchronizationAsync_Then_PersistsEachPageBeforeReadingNext(
        Guid sourceId,
        Guid synchronizationId,
        string tenantId,
        string itemId,
        string deletedItemId,
        string eTag,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        const string deltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=opaque%2Bvalue%3D%3D";
        var repository = new RecordingSynchronizationRepository(CreateList(sourceId, tenantId));
        var activeItem = CreateDeltaItem(itemId, eTag, isDeleted: false);
        var deletedItem = CreateDeltaItem(deletedItemId, eTag: null, isDeleted: true);
        var deltaClient = new OrderedDeltaClient(
            [
                new Microsoft365ListItemDeltaPage([activeItem], DeltaLink: null),
                new Microsoft365ListItemDeltaPage([activeItem, deletedItem], DeltaLink: null),
                new Microsoft365ListItemDeltaPage([], deltaLink)
            ],
            () => repository.SavedWorkPages.Count);
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var service = CreateService(repository, schemaClient, fingerprint, now, deltaClient);

        // When
        var result = await service.StartInitialSynchronizationAsync(
            sourceId,
            synchronizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ListInitialSynchronizationStatus.Completed, result.Status);
        Assert.Equal(2, result.ProcessWorkCount);
        Assert.Equal(1, result.DeleteWorkCount);
        Assert.Equal(2, result.PersistedWorkCount);
        Assert.Equal(deltaLink, result.DeltaLink);
        Assert.Equal(3, repository.SavedWorkPages.Count);
        Assert.Equal(deltaLink, repository.ConfirmedDeltaLink);
        Assert.Equal(now, repository.CheckpointConfirmedAt);
        Assert.Equal(3, repository.SavedPageCountWhenCheckpointConfirmed);
        Assert.Equal(Microsoft365SynchronizationStatus.Succeeded, repository.RecordedStatus);
        Assert.Equal(0, repository.RecordedCounters?.CreatedCount);
        Assert.Equal(2, repository.RecordedCounters?.ModifiedCount);
        Assert.Equal(1, repository.RecordedCounters?.DeletedCount);
        Assert.Equal(0, repository.RecordedCounters?.FailedCount);
        var processWork = Assert.Single(repository.SavedWorkPages[0]);
        Assert.Equal(Microsoft365ListItemWorkType.ProcessListItem, processWork.WorkType);
        Assert.Equal(itemId, processWork.ListItemId);
        Assert.Equal(eTag, processWork.ETag);
        Assert.Contains("Title", processWork.FieldsJson, StringComparison.Ordinal);
        var deleteWork = Assert.Single(
            repository.SavedWorkPages[1],
            work => work.WorkType == Microsoft365ListItemWorkType.DeleteListItem);
        Assert.Equal(deletedItemId, deleteWork.ListItemId);
        Assert.Null(deleteWork.ETag);
        Assert.Null(deleteWork.FieldsJson);
        Assert.Null(repository.ReleasedErrorCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_SecondDeltaPageFails_When_StartInitialSynchronizationAsync_Then_FirstPageRemainsPersistedAndLeaseIsReleased(
        Guid sourceId,
        Guid synchronizationId,
        string tenantId,
        string itemId,
        string eTag,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        var repository = new RecordingSynchronizationRepository(CreateList(sourceId, tenantId));
        var firstPage = new Microsoft365ListItemDeltaPage(
            [CreateDeltaItem(itemId, eTag, isDeleted: false)],
            DeltaLink: null);
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var service = CreateService(
            repository,
            schemaClient,
            fingerprint,
            now,
            new FailingDeltaClient(firstPage));

        // When
        var action = () => service.StartInitialSynchronizationAsync(
            sourceId,
            synchronizationId,
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Single(repository.SavedWorkPages);
        Assert.Null(repository.ConfirmedDeltaLink);
        Assert.Equal("MicrosoftGraphInitialListSynchronizationFailed", repository.ReleasedErrorCode);
        Assert.Equal(1, repository.ReleaseCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AReplayedActiveDeltaEvent_When_StartDeltaSynchronizationAsync_Then_UsesStoredLinkAndPersistsOneWork(
        Guid sourceId,
        Guid synchronizationId,
        string tenantId,
        string itemId,
        string eTag,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        const string storedDeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=stored%2Bopaque%3D";
        const string nextDeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=next%2Bopaque%3D";
        var list = CreateList(sourceId, tenantId);
        list.DeltaLink = storedDeltaLink;
        var repository = new RecordingSynchronizationRepository(list);
        var item = CreateDeltaItem(itemId, eTag, isDeleted: false);
        var deltaClient = new OrderedDeltaClient(
            [
                new Microsoft365ListItemDeltaPage([item], DeltaLink: null),
                new Microsoft365ListItemDeltaPage([item], nextDeltaLink)
            ],
            () => repository.SavedWorkPages.Count);
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var service = CreateService(repository, schemaClient, fingerprint, now, deltaClient);

        // When
        var result = await service.StartDeltaSynchronizationAsync(
            sourceId,
            synchronizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ListDeltaSynchronizationStatus.Completed, result.Status);
        Assert.Equal(storedDeltaLink, deltaClient.DeltaLink);
        Assert.Equal(2, result.ProcessWorkCount);
        Assert.Equal(1, result.PersistedWorkCount);
        Assert.Equal(nextDeltaLink, repository.ConfirmedDeltaLink);
    }

    [Theory, AutoDomainData]
    public async Task Given_ASecondDeltaPageFailure_When_StartDeltaSynchronizationAsync_Then_KeepsPreviousCheckpoint(
        Guid sourceId,
        Guid synchronizationId,
        string tenantId,
        string itemId,
        string eTag,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        const string storedDeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=confirmed";
        var list = CreateList(sourceId, tenantId);
        list.DeltaLink = storedDeltaLink;
        var repository = new RecordingSynchronizationRepository(list);
        var firstPage = new Microsoft365ListItemDeltaPage(
            [CreateDeltaItem(itemId, eTag, isDeleted: false)],
            DeltaLink: null);
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var service = CreateService(
            repository,
            schemaClient,
            fingerprint,
            now,
            new FailingDeltaClient(firstPage));

        // When
        var action = () => service.StartDeltaSynchronizationAsync(
            sourceId,
            synchronizationId,
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Single(repository.SavedWorkPages);
        Assert.Null(repository.ConfirmedDeltaLink);
        Assert.Equal(storedDeltaLink, list.DeltaLink);
        Assert.Equal("MicrosoftGraphDeltaListSynchronizationFailed", repository.ReleasedErrorCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_MicrosoftDeniesListAccess_When_StartDeltaSynchronizationAsync_Then_MarksSourceAccessError(
        Guid sourceId,
        Guid synchronizationId,
        string tenantId,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        var list = CreateList(sourceId, tenantId);
        list.DeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=stored";
        var repository = new RecordingSynchronizationRepository(list);
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var service = CreateService(
            repository,
            schemaClient,
            fingerprint,
            now,
            new ThrowingListDeltaClient(
                new Microsoft365SourceAccessDeniedException("Access denied.")));

        // When
        var action = () => service.StartDeltaSynchronizationAsync(
            sourceId,
            synchronizationId,
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<Microsoft365SourceAccessDeniedException>(action);
        Assert.Equal(Microsoft365SourceStatus.Error, list.Status);
        Assert.Equal(Microsoft365SynchronizationStatus.PermanentFailure, repository.RecordedStatus);
        Assert.Equal("MicrosoftGraphListAccessDenied", repository.RecordedErrorCode);
        Assert.Equal(1, repository.RecordedCounters?.FailedCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidListDeltaCheckpoint_When_StartDeltaSynchronizationAsync_Then_MarksSourceAndRunsFullResync(
        Guid sourceId,
        Guid synchronizationId,
        string tenantId,
        string itemId,
        string eTag,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        const string storedDeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=expired";
        const string nextDeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=fresh";
        var list = CreateList(sourceId, tenantId);
        list.DeltaLink = storedDeltaLink;
        var repository = new RecordingSynchronizationRepository(list);
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var deltaClient = new InvalidCheckpointThenInitialDeltaClient(
            [new Microsoft365ListItemDeltaPage([CreateDeltaItem(itemId, eTag, isDeleted: false)], nextDeltaLink)]);
        var service = CreateService(repository, schemaClient, fingerprint, now, deltaClient);

        // When
        var result = await service.StartDeltaSynchronizationAsync(
            sourceId,
            synchronizationId,
            CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ListDeltaSynchronizationStatus.Completed, result.Status);
        Assert.Equal(storedDeltaLink, deltaClient.DeltaLink);
        Assert.Equal(1, deltaClient.InitialCallCount);
        Assert.Equal(Microsoft365SourceStatus.FullResyncRequired, repository.MarkedStatus);
        Assert.Equal("MicrosoftGraphListDeltaCheckpointInvalid", repository.FullResyncRequiredErrorCode);
        Assert.Equal(1, result.ProcessWorkCount);
        Assert.Equal(nextDeltaLink, repository.ConfirmedDeltaLink);
        Assert.Equal(Microsoft365SourceStatus.Enabled, list.Status);
        Assert.Equal(1, repository.SavedPageCountWhenCheckpointConfirmed);
        Assert.Null(repository.ReleasedErrorCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACompleteDeltaReadAndLostLease_When_StartInitialSynchronizationAsync_Then_DoesNotConfirmCheckpoint(
        Guid sourceId,
        Guid synchronizationId,
        string tenantId,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        const string deltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=opaque";
        var repository = new RecordingSynchronizationRepository(CreateList(sourceId, tenantId))
        {
            CheckpointCanBeConfirmed = false
        };
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var deltaClient = new OrderedDeltaClient(
            [new Microsoft365ListItemDeltaPage([], deltaLink)],
            () => repository.SavedWorkPages.Count);
        var service = CreateService(repository, schemaClient, fingerprint, now, deltaClient);

        // When
        var action = () => service.StartInitialSynchronizationAsync(
            sourceId,
            synchronizationId,
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Single(repository.SavedWorkPages);
        Assert.Null(repository.ConfirmedDeltaLink);
        Assert.Equal("MicrosoftGraphInitialListSynchronizationFailed", repository.ReleasedErrorCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_TwoConcurrentWorkers_When_SynchronizeSchemaAsync_Then_OnlyLeaseOwnerCallsGraph(
        Guid sourceId,
        string tenantId,
        string fingerprint,
        DateTimeOffset now)
    {
        // Given
        var repository = new RecordingSynchronizationRepository(CreateList(sourceId, tenantId));
        var schemaClient = new BlockingSchemaClient();
        var service = CreateService(repository, schemaClient, fingerprint, now);

        // When
        var firstSynchronization = service.SynchronizeSchemaAsync(sourceId, CancellationToken.None);
        await schemaClient.WaitUntilCalledAsync().WaitAsync(TimeSpan.FromSeconds(1));
        var secondResult = await service.SynchronizeSchemaAsync(sourceId, CancellationToken.None);
        schemaClient.Complete();
        var firstResult = await firstSynchronization;

        // Then
        Assert.Equal(Microsoft365ListSchemaSynchronizationStatus.Initialized, firstResult.Status);
        Assert.Equal(Microsoft365ListSchemaSynchronizationStatus.AlreadyInProgress, secondResult.Status);
        Assert.Equal(1, schemaClient.CallCount);
        Assert.Equal(1, repository.ReleaseCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AChangedSchema_When_SynchronizeSchemaAsync_Then_PersistsFingerprintAndSignalsReprocessing(
        Guid sourceId,
        string tenantId,
        string previousFingerprint,
        string newFingerprint,
        DateTimeOffset now)
    {
        // Given
        var list = CreateList(sourceId, tenantId);
        list.SchemaFingerprint = previousFingerprint;
        var repository = new RecordingSynchronizationRepository(list);
        var schemaClient = new StubSchemaClient((_, _, _, _) => Task.FromResult<IReadOnlyCollection<Microsoft365ListColumn>>([]));
        var service = CreateService(repository, schemaClient, newFingerprint, now);

        // When
        var result = await service.SynchronizeSchemaAsync(sourceId, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ListSchemaSynchronizationStatus.Changed, result.Status);
        Assert.Equal(newFingerprint, repository.SavedFingerprint);
        Assert.True(repository.SavedRequiresItemReprocessing);
        Assert.Null(repository.ReleasedErrorCode);
        Assert.Equal(CancellationToken.None, repository.ReleaseCancellationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_AExistingSchemaAndGraphFailure_When_SynchronizeSchemaAsync_Then_ReleasesLeaseWithError(
        Guid sourceId,
        string tenantId,
        DateTimeOffset now)
    {
        // Given
        var repository = new RecordingSynchronizationRepository(CreateList(sourceId, tenantId));
        var schemaClient = new StubSchemaClient((_, _, _, _) =>
            Task.FromException<IReadOnlyCollection<Microsoft365ListColumn>>(
                new InvalidOperationException("Graph failed.")));
        var service = CreateService(repository, schemaClient, "fingerprint", now);

        // When
        var action = () => service.SynchronizeSchemaAsync(sourceId, CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal("MicrosoftGraphListSchemaLoadFailed", repository.ReleasedErrorCode);
        Assert.Equal(1, repository.ReleaseCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_CancelledGraphCall_When_SynchronizeSchemaAsync_Then_ReleasesLeaseWithoutUsingCancelledToken(
        Guid sourceId,
        string tenantId,
        DateTimeOffset now)
    {
        // Given
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var repository = new RecordingSynchronizationRepository(CreateList(sourceId, tenantId));
        var schemaClient = new StubSchemaClient((_, _, _, cancellationToken) =>
            Task.FromCanceled<IReadOnlyCollection<Microsoft365ListColumn>>(cancellationToken));
        var service = CreateService(repository, schemaClient, "fingerprint", now);

        // When
        var action = () => service.SynchronizeSchemaAsync(sourceId, cancellationSource.Token);

        // Then
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
        Assert.Equal("MicrosoftGraphListSchemaLoadCancelled", repository.ReleasedErrorCode);
        Assert.Equal(CancellationToken.None, repository.ReleaseCancellationToken);
    }

    private static Microsoft365ListSynchronizationService CreateService(
        RecordingSynchronizationRepository repository,
        IMicrosoft365ListSchemaClient schemaClient,
        string fingerprint,
        DateTimeOffset now,
        IMicrosoft365ListItemDeltaClient? deltaClient = null) =>
        new(
            repository,
            repository,
            schemaClient,
            deltaClient ?? new EmptyDeltaClient(),
            new StubFingerprintGenerator(fingerprint),
            new Microsoft365ListItemWorkFactory(),
            Options.Create(new Microsoft365Options { SynchronizationLeaseMinutes = 15 }),
            new FixedTimeProvider(now));

    private static Microsoft365List CreateList(Guid sourceId, string tenantId) =>
        new()
        {
            Id = sourceId,
            OrganizationId = Guid.NewGuid(),
            SiteId = "site-id",
            ListId = "list-id",
            IsIndexed = true,
            Status = Microsoft365SourceStatus.Enabled,
            Microsoft365Connection = new Microsoft365Connection
            {
                TenantId = tenantId,
                Status = Microsoft365ConnectionStatus.Active,
                OrganizationConnector = new OrganizationConnector
                {
                    Status = RecordStatus.Active,
                    IsConfigured = true
                }
            }
        };

    private sealed class RecordingSynchronizationRepository(Microsoft365List list)
        : IMicrosoft365ListSynchronizationRepository, IMicrosoft365SourceSynchronizationRepository
    {
        private int leaseState;

        public string? SavedFingerprint { get; private set; }

        public bool SavedRequiresItemReprocessing { get; private set; }

        public int ReleaseCount { get; private set; }

        public string? ReleasedErrorCode { get; private set; }

        public CancellationToken ReleaseCancellationToken { get; private set; }

        public bool CheckpointCanBeConfirmed { get; init; } = true;

        public string? ConfirmedDeltaLink { get; private set; }

        public DateTimeOffset? CheckpointConfirmedAt { get; private set; }

        public int? SavedPageCountWhenCheckpointConfirmed { get; private set; }

        public Microsoft365SourceStatus? MarkedStatus { get; private set; }

        public string? FullResyncRequiredErrorCode { get; private set; }

        public Microsoft365SynchronizationStatus? RecordedStatus { get; private set; }

        public Microsoft365SynchronizationCounters? RecordedCounters { get; private set; }

        public string? RecordedErrorCode { get; private set; }

        public List<IReadOnlyCollection<Microsoft365ListItemWorkData>> SavedWorkPages { get; } = [];

        private HashSet<string> PersistedDeduplicationKeys { get; } = new(StringComparer.Ordinal);

        public Task<Microsoft365List?> FindForSynchronizationAsync(
            Guid sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Microsoft365List?>(list.Id == sourceId ? list : null);

        public Task<bool> TryAcquireLeaseAsync(
            Guid sourceId,
            Guid leaseId,
            DateTimeOffset attemptedAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Interlocked.CompareExchange(ref leaseState, 1, 0) == 0);

        public Task<bool> SaveSchemaAsync(
            Guid sourceId,
            Guid leaseId,
            string schemaFingerprint,
            bool requiresItemReprocessing,
            CancellationToken cancellationToken = default)
        {
            SavedFingerprint = schemaFingerprint;
            SavedRequiresItemReprocessing = requiresItemReprocessing;
            return Task.FromResult(Volatile.Read(ref leaseState) == 1);
        }

        public Task<bool> ConfirmCheckpointAsync(
            Guid sourceId,
            Guid leaseId,
            string deltaLink,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken = default)
        {
            if (!CheckpointCanBeConfirmed || Volatile.Read(ref leaseState) != 1)
            {
                return Task.FromResult(false);
            }

            ConfirmedDeltaLink = deltaLink;
            CheckpointConfirmedAt = completedAt;
            SavedPageCountWhenCheckpointConfirmed = SavedWorkPages.Count;
            list.DeltaLink = deltaLink;
            if (list.Status == Microsoft365SourceStatus.FullResyncRequired)
            {
                list.Status = Microsoft365SourceStatus.Enabled;
            }

            return Task.FromResult(true);
        }

        public Task<bool> MarkFullResyncRequiredAsync(
            Guid sourceId,
            Guid leaseId,
            string lastErrorCode,
            CancellationToken cancellationToken = default)
        {
            list.Status = Microsoft365SourceStatus.FullResyncRequired;
            MarkedStatus = list.Status;
            FullResyncRequiredErrorCode = lastErrorCode;
            return Task.FromResult(Volatile.Read(ref leaseState) == 1);
        }

        public Task<bool> MarkAccessErrorAsync(
            Guid sourceId,
            Guid leaseId,
            string lastErrorCode,
            CancellationToken cancellationToken = default)
        {
            list.Status = Microsoft365SourceStatus.Error;
            ReleasedErrorCode = lastErrorCode;
            return Task.FromResult(Volatile.Read(ref leaseState) == 1);
        }

        public Task<bool> RecordSynchronizationOutcomeAsync(
            Guid sourceId,
            Guid synchronizationId,
            Microsoft365SynchronizationStatus status,
            Microsoft365SynchronizationCounters counters,
            DateTimeOffset completedAt,
            string? lastErrorCode,
            CancellationToken cancellationToken = default)
        {
            RecordedStatus = status;
            RecordedCounters = counters;
            RecordedErrorCode = lastErrorCode;
            return Task.FromResult(true);
        }

        public Task<int> SaveWorkPageAsync(
            Guid sourceId,
            Guid synchronizationId,
            Guid leaseId,
            DateTimeOffset leaseExpiresAt,
            IReadOnlyCollection<Microsoft365ListItemWorkData> works,
            CancellationToken cancellationToken = default)
        {
            SavedWorkPages.Add(works.ToArray());
            var persistedCount = works.Count(work =>
                PersistedDeduplicationKeys.Add(work.DeduplicationKey));
            return Task.FromResult(persistedCount);
        }

        public Task ReleaseLeaseAsync(
            Guid sourceId,
            Guid leaseId,
            string? lastErrorCode,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Exchange(ref leaseState, 0);
            ReleaseCount++;
            ReleasedErrorCode = lastErrorCode;
            ReleaseCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSchemaClient(
        Func<string, string, string, CancellationToken, Task<IReadOnlyCollection<Microsoft365ListColumn>>> action)
        : IMicrosoft365ListSchemaClient
    {
        public Task<IReadOnlyCollection<Microsoft365ListColumn>> GetColumnsAsync(
            string tenantId,
            string siteId,
            string listId,
            CancellationToken cancellationToken = default) =>
            action(tenantId, siteId, listId, cancellationToken);
    }

    private sealed class BlockingSchemaClient : IMicrosoft365ListSchemaClient
    {
        private readonly TaskCompletionSource called = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public Task WaitUntilCalledAsync() => called.Task;

        public void Complete() => completed.TrySetResult();

        public async Task<IReadOnlyCollection<Microsoft365ListColumn>> GetColumnsAsync(
            string tenantId,
            string siteId,
            string listId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            called.TrySetResult();
            await completed.Task.WaitAsync(cancellationToken);
            return [];
        }
    }

    private sealed class StubFingerprintGenerator(string fingerprint)
        : IMicrosoft365ListSchemaFingerprintGenerator
    {
        public string CreateFingerprint(IReadOnlyCollection<Microsoft365ListColumn> columns) => fingerprint;
    }

    private sealed class EmptyDeltaClient : IMicrosoft365ListItemDeltaClient
    {
        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetInitialPagesAsync(
            string tenantId,
            string siteId,
            string listId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetDeltaPagesAsync(
            string tenantId,
            string deltaLink,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class OrderedDeltaClient(
        IReadOnlyCollection<Microsoft365ListItemDeltaPage> pages,
        Func<int> persistedPageCount) : IMicrosoft365ListItemDeltaClient
    {
        public string? DeltaLink { get; private set; }

        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetInitialPagesAsync(
            string tenantId,
            string siteId,
            string listId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var pageIndex = 0;
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.Equal(pageIndex, persistedPageCount());
                yield return page;
                pageIndex++;
            }

            await Task.CompletedTask;
        }

        public IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetDeltaPagesAsync(
            string tenantId,
            string deltaLink,
            CancellationToken cancellationToken = default)
        {
            DeltaLink = deltaLink;
            return GetInitialPagesAsync(tenantId, string.Empty, string.Empty, cancellationToken);
        }
    }

    private sealed class FailingDeltaClient(Microsoft365ListItemDeltaPage firstPage)
        : IMicrosoft365ListItemDeltaClient
    {
        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetInitialPagesAsync(
            string tenantId,
            string siteId,
            string listId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return firstPage;
            await Task.Yield();
            throw new InvalidOperationException("The second delta page failed.");
        }

        public IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetDeltaPagesAsync(
            string tenantId,
            string deltaLink,
            CancellationToken cancellationToken = default) =>
            GetInitialPagesAsync(tenantId, string.Empty, string.Empty, cancellationToken);
    }

    private sealed class InvalidCheckpointThenInitialDeltaClient(
        IReadOnlyCollection<Microsoft365ListItemDeltaPage> initialPages)
        : IMicrosoft365ListItemDeltaClient
    {
        public int InitialCallCount { get; private set; }

        public string? DeltaLink { get; private set; }

        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetInitialPagesAsync(
            string tenantId,
            string siteId,
            string listId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            InitialCallCount++;
            foreach (var page in initialPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return page;
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetDeltaPagesAsync(
            string tenantId,
            string deltaLink,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            DeltaLink = deltaLink;
            await Task.Yield();
            throw new Microsoft365DeltaCheckpointInvalidException("Delta checkpoint is invalid.");
            #pragma warning disable CS0162
            yield break;
            #pragma warning restore CS0162
        }
    }

    private sealed class ThrowingListDeltaClient(Exception exception)
        : IMicrosoft365ListItemDeltaClient
    {
        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetInitialPagesAsync(
            string tenantId,
            string siteId,
            string listId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw exception;
            #pragma warning disable CS0162
            yield break;
            #pragma warning restore CS0162
        }

        public async IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetDeltaPagesAsync(
            string tenantId,
            string deltaLink,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw exception;
            #pragma warning disable CS0162
            yield break;
            #pragma warning restore CS0162
        }
    }

    private static Microsoft365ListItemDelta CreateDeltaItem(
        string itemId,
        string? eTag,
        bool isDeleted)
    {
        JsonElement? fields = null;
        if (!isDeleted)
        {
            using var document = JsonDocument.Parse("{\"Title\":\"Request\"}");
            fields = document.RootElement.Clone();
        }

        return new Microsoft365ListItemDelta(
            itemId,
            eTag,
            DateTimeOffset.Parse("2026-08-01T10:00:00Z"),
            DateTimeOffset.Parse("2026-08-02T11:00:00Z"),
            "https://contoso/items/1",
            fields,
            isDeleted);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
