using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Caching.Memory;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ResetServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_CachedCompletedOnboarding_When_ResetAsync_Then_CacheIsInvalidated(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Role = OrganizationRole.Admin;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheKey = Microsoft365OnboardingCacheKeys.CompletionKey(organization.Id);
        cache.Set(cacheKey, true);
        var service = CreateService(organization, member, cache, out _, out _);

        // When
        await service.ResetSelectionAndIndexingAsync(CancellationToken.None);

        // Then
        Assert.False(cache.TryGetValue(cacheKey, out bool _));
    }

    [Theory, AutoDomainData]
    public async Task Given_IndexedChunks_When_ResetAsync_Then_SearchChunksAreDeletedBeforeTheRows(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Role = OrganizationRole.Admin;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(
            organization,
            member,
            cache,
            out var resetRepository,
            out var indexWriter);

        // When
        var result = await service.ResetSelectionAndIndexingAsync(CancellationToken.None);

        // Then
        Assert.Equal(["chunk-1", "chunk-2"], indexWriter.DeletedChunkIds);
        Assert.Equal(2, result.DeletedSearchChunks);
        Assert.Equal(
            ["DeleteSearchChunks", "ResetRows"],
            resetRepository.Operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_ANonAdministratorMember_When_ResetAsync_Then_ThrowsBeforeTouchingAnything(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Role = OrganizationRole.User;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(
            organization,
            member,
            cache,
            out var resetRepository,
            out var indexWriter);

        // When / Then
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ResetSelectionAndIndexingAsync(CancellationToken.None));
        Assert.Empty(resetRepository.Operations);
        Assert.Empty(indexWriter.DeletedChunkIds);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInactiveConnection_When_ResetAsync_Then_ThrowsAConflictWithAStableCode(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Role = OrganizationRole.Admin;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var connectionRepository = new StubConnectionRepository(
            CreateConnection(organization.Id, Microsoft365ConnectionStatus.Revoked));
        var resetRepository = new RecordingResetRepository();
        var service = new Microsoft365ResetService(
            new StubAuthenticateUserService { Result = (organization, member) },
            connectionRepository,
            resetRepository,
            new RecordingPassageIndexWriter(),
            cache);

        // When
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ResetSelectionAndIndexingAsync(CancellationToken.None));

        // Then
        Assert.Equal(ConflictException.Microsoft365ConnectionInactive, exception.ErrorCode);
        Assert.Empty(resetRepository.Operations);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoConnection_When_ResetAsync_Then_ThrowsNotFound(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        member.OrganizationId = organization.Id;
        member.Role = OrganizationRole.Admin;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new Microsoft365ResetService(
            new StubAuthenticateUserService { Result = (organization, member) },
            new StubConnectionRepository(connection: null),
            new RecordingResetRepository(),
            new RecordingPassageIndexWriter(),
            cache);

        // When / Then
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ResetSelectionAndIndexingAsync(CancellationToken.None));
    }

    private static Microsoft365ResetService CreateService(
        Organization organization,
        OrganizationMember member,
        IMemoryCache cache,
        out RecordingResetRepository resetRepository,
        out RecordingPassageIndexWriter indexWriter)
    {
        resetRepository = new RecordingResetRepository
        {
            ChunkIds = ["chunk-1", "chunk-2"]
        };
        indexWriter = new RecordingPassageIndexWriter();

        return new Microsoft365ResetService(
            new StubAuthenticateUserService { Result = (organization, member) },
            new StubConnectionRepository(
                CreateConnection(organization.Id, Microsoft365ConnectionStatus.Active)),
            resetRepository,
            indexWriter,
            cache);
    }

    private static Microsoft365Connection CreateConnection(
        Guid organizationId,
        Microsoft365ConnectionStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OrganizationConnectorId = Guid.NewGuid(),
            TenantId = "tenant",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private sealed class StubConnectionRepository(Microsoft365Connection? connection)
        : IMicrosoft365ConnectionRepository
    {
        public Task<Microsoft365Connection?> FindByOrganizationAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(connection);

        public Task<Microsoft365Connection> PrepareConsentAsync(Guid organizationId, string stateHash, DateTimeOffset stateExpiresAt, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Microsoft365Connection?> FindConsentAsync(Guid organizationId, string stateHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsTenantConnectedToAnotherOrganizationAsync(Guid organizationId, string tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Microsoft365Connection?> FindByIdAsync(Guid connectionId, Guid organizationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Microsoft365Connection?> FindForProcessingAsync(Guid connectionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteConsentAsync(Microsoft365Connection connection, string tenantId, DateTimeOffset completedAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkConsentErrorAsync(Microsoft365Connection connection, string errorCode, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAsync(Microsoft365Connection connection, DateTimeOffset occurredAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingResetRepository : IMicrosoft365ResetRepository
    {
        public List<string> Operations { get; } = [];

        public IReadOnlyCollection<string> ChunkIds { get; init; } = [];

        public Task<IReadOnlyCollection<string>> GetIndexedChunkIdsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("DeleteSearchChunks");
            return Task.FromResult(ChunkIds);
        }

        public Task<Microsoft365ResetCounts> ResetSelectionAndIndexingAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            Operations.Add("ResetRows");
            return Task.FromResult(new Microsoft365ResetCounts(1, 1, 1, 1, 1, 2));
        }
    }

    private sealed class RecordingPassageIndexWriter : IMicrosoft365PassageIndexWriter
    {
        public List<string> DeletedChunkIds { get; } = [];

        public Task MergeOrUploadAsync(
            Guid organizationId,
            IReadOnlyCollection<Microsoft365SearchPassage> passages,
            Microsoft365Acl acl,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            IReadOnlyCollection<string> chunkIds,
            CancellationToken cancellationToken = default)
        {
            DeletedChunkIds.AddRange(chunkIds);
            return Task.CompletedTask;
        }
    }
}
