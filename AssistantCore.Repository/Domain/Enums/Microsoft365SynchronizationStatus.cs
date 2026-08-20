namespace AssistantCore.Repository.Domain.Enums;

public enum Microsoft365SynchronizationStatus
{
    Pending,
    Running,
    Succeeded,
    TemporaryFailure,
    PermanentFailure,
    Cancelled
}
