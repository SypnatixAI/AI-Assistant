namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365PendingSynchronizationRepository
{
    Task<Microsoft365PendingSynchronization?> ClaimNextAsync(
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default);
}
