namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365PendingSynchronizationService
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}
