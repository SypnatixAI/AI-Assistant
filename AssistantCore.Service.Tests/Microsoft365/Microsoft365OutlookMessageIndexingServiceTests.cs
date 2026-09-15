using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365OutlookMessageIndexingServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnOutlookMessage_When_IndexAsync_Then_RestrictsAclToMailboxUserAndKeepsOrganizationIsolation(
        Guid organizationId,
        Guid sourceId,
        string mailboxUserId,
        string normalizedMailboxUserId,
        string messageId,
        DateTimeOffset modifiedAt)
    {
        var organization = new Organization { Id = organizationId, Name = "Contoso" };
        var normalizer = new StubIdentityNormalizer(normalizedMailboxUserId);
        var indexWriter = new RecordingPassageIndexWriter();
        var aclService = new RecordingAclSynchronizationService();
        var service = new Microsoft365OutlookMessageIndexingService(
            normalizer,
            new StubEmbeddingGenerator(),
            indexWriter,
            aclService);
        var message = new Microsoft365OutlookMessageDelta(
            messageId,
            "Quarterly results",
            "Revenue increased.",
            "https://outlook.office.com/mail/message",
            modifiedAt.AddMinutes(-10),
            modifiedAt,
            modifiedAt,
            IsDeleted: false);

        await service.IndexAsync(
            organization,
            sourceId,
            mailboxUserId,
            message,
            CancellationToken.None);

        Assert.Equal(mailboxUserId, normalizer.ReceivedUserId);
        Assert.Equal(organizationId, indexWriter.OrganizationId);
        Assert.Equal([normalizedMailboxUserId], indexWriter.Acl?.UserIds);
        Assert.Empty(indexWriter.Acl?.GroupIds ?? []);
        Assert.Empty(indexWriter.Acl?.SharePointGroupIds ?? []);
        Assert.False(indexWriter.Acl?.HasAnonymousLink);
        Assert.False(indexWriter.Acl?.HasOrganizationLink);
        var passage = Assert.Single(indexWriter.Passages);
        Assert.Equal(messageId, passage.DriveItemId);
        Assert.Equal("outlook", passage.SourceType);
        Assert.Equal("Revenue increased.", passage.Content);
        Assert.Equal((organizationId, sourceId, messageId), aclService.RegisteredResource);
        Assert.Equal((organizationId, sourceId, messageId), aclService.SynchronizedResource);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnOutlookMessageDeletion_When_DeleteAsync_Then_DeletesOnlyThatResourceAndMarksItsAclUnavailable(
        Guid organizationId,
        Guid sourceId,
        string messageId)
    {
        var indexWriter = new RecordingPassageIndexWriter();
        var aclService = new RecordingAclSynchronizationService();
        var service = new Microsoft365OutlookMessageIndexingService(
            new StubIdentityNormalizer("user:normalized"),
            new StubEmbeddingGenerator(),
            indexWriter,
            aclService);

        await service.DeleteAsync(organizationId, sourceId, messageId, CancellationToken.None);

        Assert.Single(indexWriter.DeletedChunkIds);
        Assert.Equal((organizationId, sourceId, messageId), aclService.UnavailableResource);
    }

    private sealed class StubIdentityNormalizer(string normalizedUserId) : IMicrosoft365SecurityIdentityNormalizer
    {
        public string? ReceivedUserId { get; private set; }

        public string NormalizeEntraUserId(string objectId)
        {
            ReceivedUserId = objectId;
            return normalizedUserId;
        }

        public string NormalizeEntraGroupId(string objectId) => $"group:{objectId}";
        public string NormalizeEntraGroupOwnerId(string objectId) => $"owner:{objectId}";
        public string NormalizeSharePointGroupId(string siteId, string sharePointGroupId) => $"sp:{siteId}:{sharePointGroupId}";
    }

    private sealed class StubEmbeddingGenerator : IMicrosoft365EmbeddingGenerator
    {
        public Task<IReadOnlyList<IReadOnlyList<float>>> CreateAsync(
            IReadOnlyCollection<string> contents,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(
                contents.Select(_ => (IReadOnlyList<float>)[0.1f, 0.2f]).ToArray());
    }

    private sealed class RecordingPassageIndexWriter : IMicrosoft365PassageIndexWriter
    {
        public Guid? OrganizationId { get; private set; }
        public IReadOnlyCollection<Microsoft365SearchPassage> Passages { get; private set; } = [];
        public Microsoft365Acl? Acl { get; private set; }
        public IReadOnlyCollection<string> DeletedChunkIds { get; private set; } = [];

        public Task MergeOrUploadAsync(
            Guid organizationId,
            IReadOnlyCollection<Microsoft365SearchPassage> passages,
            Microsoft365Acl acl,
            CancellationToken cancellationToken = default)
        {
            OrganizationId = organizationId;
            Passages = passages;
            Acl = acl;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            IReadOnlyCollection<string> chunkIds,
            CancellationToken cancellationToken = default)
        {
            DeletedChunkIds = chunkIds;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAclSynchronizationService : IMicrosoft365ContentAclSynchronizationService
    {
        public (Guid OrganizationId, Guid SourceId, string ExternalContentId)? RegisteredResource { get; private set; }
        public (Guid OrganizationId, Guid SourceId, string ExternalContentId)? SynchronizedResource { get; private set; }
        public (Guid OrganizationId, Guid SourceId, string ExternalContentId)? UnavailableResource { get; private set; }

        public Task RegisterAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            IReadOnlyCollection<string> chunkIds,
            string aclFingerprint,
            string? siteUrl,
            CancellationToken cancellationToken = default)
        {
            RegisteredResource = (organizationId, sourceId, externalContentId);
            return Task.CompletedTask;
        }

        public Task<bool> MarkUnavailableIfRegisteredAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            CancellationToken cancellationToken = default)
        {
            UnavailableResource = (organizationId, sourceId, externalContentId);
            return Task.FromResult(true);
        }

        public Task<Microsoft365AclSynchronizationResult> SynchronizeIfRegisteredAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            Microsoft365Acl acl,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Microsoft365AclSynchronizationResult.Unchanged);

        public Task<Microsoft365AclSynchronizationResult> SynchronizeAsync(
            Guid organizationId,
            Guid sourceId,
            string externalContentId,
            Microsoft365Acl acl,
            CancellationToken cancellationToken = default)
        {
            SynchronizedResource = (organizationId, sourceId, externalContentId);
            return Task.FromResult(Microsoft365AclSynchronizationResult.Updated);
        }
    }
}
