using System.Security.Cryptography;
using System.Text;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365OutlookMessageIndexingService(
    IMicrosoft365SecurityIdentityNormalizer identityNormalizer,
    IMicrosoft365EmbeddingGenerator embeddingGenerator,
    IMicrosoft365PassageIndexWriter indexWriter,
    IMicrosoft365ContentAclSynchronizationService aclSynchronizationService)
    : IMicrosoft365OutlookMessageIndexingService
{
    public async Task IndexAsync(
        Organization organization,
        Guid sourceId,
        string mailboxUserId,
        Microsoft365OutlookMessageDelta message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(message);
        if (organization.Id == Guid.Empty || sourceId == Guid.Empty)
        {
            throw new ArgumentException("Organization and source identifiers are required.");
        }

        if (message.IsDeleted || string.IsNullOrWhiteSpace(message.BodyContent))
        {
            await DeleteAsync(organization.Id, sourceId, message.Id, cancellationToken);
            return;
        }

        var normalizedMailboxUserId = identityNormalizer.NormalizeEntraUserId(mailboxUserId);
        var acl = new Microsoft365Acl(
            [normalizedMailboxUserId],
            [],
            [],
            hasAnonymousLink: false,
            hasOrganizationLink: false,
            Microsoft365AclInheritance.Unique);

        var content = message.BodyContent.Trim();
        var embeddings = await embeddingGenerator.CreateAsync([content], cancellationToken);
        if (embeddings.Count != 1)
        {
            throw new InvalidOperationException("Outlook message embedding generation returned an unexpected result.");
        }

        var chunkId = CreateChunkId(sourceId, message.Id);
        var passage = new Microsoft365SearchPassage(
            chunkId,
            string.IsNullOrWhiteSpace(message.Subject) ? "Courriel Outlook" : message.Subject.Trim(),
            content,
            DriveItemId: message.Id,
            DocumentVersion: message.LastModifiedDateTime?.ToString("O"),
            Url: message.WebLink,
            ModifiedAt: message.LastModifiedDateTime ?? message.ReceivedDateTime,
            ContentVector: embeddings[0],
            SourceType: "outlook");

        await aclSynchronizationService.SynchronizeIfRegisteredAsync(
            organization.Id,
            sourceId,
            message.Id,
            acl,
            cancellationToken);
        await indexWriter.MergeOrUploadAsync(
            organization.Id,
            [passage],
            acl,
            cancellationToken);
        await aclSynchronizationService.RegisterAsync(
            organization.Id,
            sourceId,
            message.Id,
            [chunkId],
            acl.Fingerprint,
            siteUrl: null,
            cancellationToken);
        await aclSynchronizationService.SynchronizeAsync(
            organization.Id,
            sourceId,
            message.Id,
            acl,
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid organizationId,
        Guid sourceId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || sourceId == Guid.Empty || string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Organization, source and Outlook message identifiers are required.");
        }

        await indexWriter.DeleteAsync([CreateChunkId(sourceId, messageId)], cancellationToken);
        await aclSynchronizationService.MarkUnavailableIfRegisteredAsync(
            organizationId,
            sourceId,
            messageId,
            cancellationToken);
    }

    private static string CreateChunkId(Guid sourceId, string messageId)
    {
        var payload = Encoding.UTF8.GetBytes($"outlook:{sourceId:N}:{messageId}");
        return Convert.ToHexString(SHA256.HashData(payload));
    }
}
