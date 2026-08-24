using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365PassageIndexingService(
    IMicrosoft365PassageIndexWriter indexWriter,
    IMicrosoft365AclResolver aclResolver,
    IMicrosoft365ContentAclSynchronizationService aclSynchronizationService)
    : IMicrosoft365PassageIndexingService
{
    public async Task<Microsoft365PassageIndexingResult> IndexAsync(
        Organization organization,
        Guid sourceId,
        Microsoft365ContentReference contentReference,
        IReadOnlyCollection<Microsoft365SearchPassage> passages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(contentReference);
        if (organization.Id == Guid.Empty || sourceId == Guid.Empty)
        {
            throw new ArgumentException("Organization and source identifiers are required.");
        }

        var aclResolution = await aclResolver.ResolveAsync(
            organization,
            contentReference,
            cancellationToken);
        if (aclResolution is Microsoft365AclResolution.Unresolved)
        {
            await aclSynchronizationService.MarkUnavailableIfRegisteredAsync(
                organization.Id,
                sourceId,
                contentReference.ItemId,
                cancellationToken);
            return Microsoft365PassageIndexingResult.SkippedUnresolvedAcl;
        }

        var normalizedPassages = NormalizePassages(passages);
        var acl = ((Microsoft365AclResolution.ResolvedAcl)aclResolution).Acl;

        await aclSynchronizationService.SynchronizeIfRegisteredAsync(
            organization.Id,
            sourceId,
            contentReference.ItemId,
            acl,
            cancellationToken);

        await indexWriter.MergeOrUploadAsync(
            organization.Id,
            normalizedPassages,
            acl,
            cancellationToken);
        await aclSynchronizationService.RegisterAsync(
            organization.Id,
            sourceId,
            contentReference.ItemId,
            normalizedPassages.Select(passage => passage.ChunkId).ToArray(),
            acl.Fingerprint,
            contentReference.SiteUrl,
            cancellationToken);
        await aclSynchronizationService.SynchronizeAsync(
            organization.Id,
            sourceId,
            contentReference.ItemId,
            acl,
            cancellationToken);

        return Microsoft365PassageIndexingResult.Indexed;
    }

    private static Microsoft365SearchPassage[] NormalizePassages(
        IReadOnlyCollection<Microsoft365SearchPassage> passages)
    {
        ArgumentNullException.ThrowIfNull(passages);
        if (passages.Count == 0
            || passages.Any(passage =>
                passage is null
                || string.IsNullOrWhiteSpace(passage.ChunkId)
                || string.IsNullOrWhiteSpace(passage.Title)
                || string.IsNullOrWhiteSpace(passage.Content)))
        {
            throw new ArgumentException(
                "At least one complete search passage is required.",
                nameof(passages));
        }

        var normalized = passages
            .GroupBy(passage => passage.ChunkId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(passage => passage.ChunkId, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length != passages.Count)
        {
            throw new ArgumentException("Search passage identifiers must be unique.", nameof(passages));
        }

        return normalized;
    }
}
