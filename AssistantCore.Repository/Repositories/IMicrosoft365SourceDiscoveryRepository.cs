using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365SourceDiscoveryRepository
{
    Task<Microsoft365Site?> FindSiteAsync(
        Guid organizationId,
        string siteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(
        Guid organizationId,
        string siteId,
        CancellationToken cancellationToken = default);

    Task<Microsoft365List?> FindListAsync(
        Guid organizationId,
        string siteId,
        string listId,
        CancellationToken cancellationToken = default);

    Task<Microsoft365ListIndexingRequestCounts> SaveListActivationAsync(
        Microsoft365List list,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);

    Task<Microsoft365ListIndexingRequestCounts> SaveListDeactivationAsync(
        Microsoft365List list,
        DateTimeOffset requestedAt,
        bool requestIndexCleanup,
        CancellationToken cancellationToken = default);

    Task ReconcileSiteSourcesAsync(
        Microsoft365Site site,
        IReadOnlyCollection<Microsoft365SourceDiscoveryData> drives,
        IReadOnlyCollection<Microsoft365SourceDiscoveryData> lists,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken = default);
}
