namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DocumentProcessingService
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}
