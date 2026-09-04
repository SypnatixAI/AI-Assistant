using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365PassageIndexingServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnUnresolvedAcl_When_IndexAsync_Then_DoesNotCallMergeOrUpload(
        Guid organizationId,
        Guid sourceId,
        string externalContentId,
        string chunkId,
        string title,
        string content)
    {
        // Given
        var writer = new RecordingIndexWriter();
        var aclSynchronization = new RecordingAclSynchronizationService();
        var resolution = new Microsoft365AclResolution.Unresolved(
            Microsoft365AclResolutionFailureReason.UnknownPrincipal);
        var service = new Microsoft365PassageIndexingService(
            writer,
            new AclResolverFake(resolution),
            aclSynchronization);
        var organization = new Organization { Id = organizationId };
        var reference = CreateReference(externalContentId);

        // When
        var result = await service.IndexAsync(
            organization,
            sourceId,
            reference,
            [new Microsoft365SearchPassage(chunkId, title, content)]);

        // Then
        Assert.Equal(Microsoft365PassageIndexingResult.SkippedUnresolvedAcl, result);
        Assert.Equal(0, writer.CallCount);
        Assert.Equal(0, aclSynchronization.RegisterCallCount);
        Assert.Equal(0, aclSynchronization.SynchronizeCallCount);
        Assert.Equal(1, aclSynchronization.MarkUnavailableCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AResolvedAcl_When_IndexAsync_Then_WritesRegistersAndPublishesPassages(
        Guid organizationId,
        Guid sourceId,
        string externalContentId,
        string chunkId,
        string title,
        string content,
        string userId)
    {
        // Given
        var acl = new Microsoft365Acl(
            [userId],
            [],
            [],
            false,
            false,
            Microsoft365AclInheritance.Unique);
        var operations = new List<string>();
        var writer = new RecordingIndexWriter(operations);
        var aclSynchronization = new RecordingAclSynchronizationService(operations);
        var service = new Microsoft365PassageIndexingService(
            writer,
            new AclResolverFake(new Microsoft365AclResolution.ResolvedAcl(acl)),
            aclSynchronization);
        var organization = new Organization { Id = organizationId };

        // When
        var result = await service.IndexAsync(
            organization,
            sourceId,
            CreateReference(externalContentId),
            [new Microsoft365SearchPassage(chunkId, title, content)]);

        // Then
        Assert.Equal(Microsoft365PassageIndexingResult.Indexed, result);
        Assert.Equal(1, writer.CallCount);
        Assert.Equal(organizationId, writer.OrganizationId);
        Assert.Same(acl, writer.Acl);
        Assert.Equal(1, aclSynchronization.RegisterCallCount);
        Assert.Equal(acl.Fingerprint, aclSynchronization.RegisteredFingerprint);
        Assert.Equal(1, aclSynchronization.SynchronizeCallCount);
        Assert.Equal(1, aclSynchronization.SynchronizeIfRegisteredCallCount);
        Assert.Equal(
            ["reconcile-existing-acl", "merge-content", "register-content", "publish-content"],
            operations);
    }

    private static Microsoft365ContentReference CreateReference(string itemId) => new(
        Microsoft365ContentReferenceKind.DriveItem,
        "site-id",
        "drive-id",
        ListId: null,
        itemId);

    private sealed class AclResolverFake(Microsoft365AclResolution resolution)
        : IMicrosoft365AclResolver
    {
        public Task<Microsoft365AclResolution> ResolveAsync(
            Organization organization,
            Microsoft365ContentReference contentReference,
            CancellationToken cancellationToken = default) => Task.FromResult(resolution);
    }

    private sealed class RecordingIndexWriter(List<string>? operations = null)
        : IMicrosoft365PassageIndexWriter
    {
        public int CallCount { get; private set; }
        public Guid OrganizationId { get; private set; }
        public Microsoft365Acl? Acl { get; private set; }

        public Task MergeOrUploadAsync(
            Guid organizationId,
            IReadOnlyCollection<Microsoft365SearchPassage> passages,
            Microsoft365Acl acl,
            CancellationToken cancellationToken = default)
        {
            operations?.Add("merge-content");
            CallCount++;
            OrganizationId = organizationId;
            Acl = acl;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAclSynchronizationService(List<string>? operations = null)
        : IMicrosoft365ContentAclSynchronizationService
    {
        public int RegisterCallCount { get; private set; }
        public int SynchronizeCallCount { get; private set; }
        public int SynchronizeIfRegisteredCallCount { get; private set; }
        public int MarkUnavailableCallCount { get; private set; }
        public string? RegisteredFingerprint { get; private set; }

        public Task RegisterAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            IReadOnlyCollection<string> chunkIds,
            string aclFingerprint,
            string? siteUrl,
            CancellationToken cancellationToken = default)
        {
            operations?.Add("register-content");
            RegisterCallCount++;
            RegisteredFingerprint = aclFingerprint;
            return Task.CompletedTask;
        }

        public Task<bool> MarkUnavailableIfRegisteredAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            CancellationToken cancellationToken = default)
        {
            MarkUnavailableCallCount++;
            return Task.FromResult(true);
        }

        public Task<Microsoft365AclSynchronizationResult> SynchronizeIfRegisteredAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            Microsoft365Acl acl,
            CancellationToken cancellationToken = default)
        {
            operations?.Add("reconcile-existing-acl");
            SynchronizeIfRegisteredCallCount++;
            return Task.FromResult(Microsoft365AclSynchronizationResult.Updated);
        }

        public Task<Microsoft365AclSynchronizationResult> SynchronizeAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            Microsoft365Acl acl,
            CancellationToken cancellationToken = default)
        {
            operations?.Add("publish-content");
            SynchronizeCallCount++;
            return Task.FromResult(Microsoft365AclSynchronizationResult.Published);
        }
    }
}
