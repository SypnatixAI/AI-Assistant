namespace AssistantCore.Repository.Repositories;

public sealed record Microsoft365SynchronizationCounters(
    int CreatedCount,
    int ModifiedCount,
    int DeletedCount,
    int IgnoredCount,
    int FailedCount)
{
    public static Microsoft365SynchronizationCounters Empty { get; } = new(0, 0, 0, 0, 0);
}
