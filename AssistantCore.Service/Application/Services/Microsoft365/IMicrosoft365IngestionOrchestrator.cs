namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365IngestionOrchestrator
{
    Task ScheduleInitialSynchronizationAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);
}
