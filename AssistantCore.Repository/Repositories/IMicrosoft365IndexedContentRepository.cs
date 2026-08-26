using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365IndexedContentRepository
{
    Task<Microsoft365IndexedContent?> FindAsync(
        Guid organizationId,
        Guid sourceId,
        string externalContentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Microsoft365IndexedContent>> GetAclReconciliationCandidatesAsync(
        DateTimeOffset dueAt,
        int maximumResults,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Microsoft365IndexedContent>> GetBySourceAsync(
        Guid organizationId,
        Guid sourceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Microsoft365IndexedContent>>([]);

    Task RequestAclReconciliationAsync(
        Guid sourceId,
        DateTimeOffset dueAt,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Microsoft365IndexedContent content,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Microsoft365IndexedContent content,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Indexed content deletion is not implemented."));
}
