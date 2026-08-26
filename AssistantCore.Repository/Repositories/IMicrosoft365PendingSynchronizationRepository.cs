namespace AssistantCore.Repository.Repositories;

public interface IMicrosoft365PendingSynchronizationRepository
{
    Task<Microsoft365PendingSynchronization?> ClaimNextDriveAsync(
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default);
}
