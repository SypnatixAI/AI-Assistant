namespace AssistantCore.Repository.Repositories;

public sealed record Microsoft365ListIndexingRequestCounts(
    int InitialSynchronizationRequests,
    int CancelledIngestionJobs,
    int SubscriptionCreationRequests,
    int SubscriptionStopRequests,
    int IndexCleanupRequests)
{
    public static Microsoft365ListIndexingRequestCounts Empty { get; } =
        new(0, 0, 0, 0, 0);
}
