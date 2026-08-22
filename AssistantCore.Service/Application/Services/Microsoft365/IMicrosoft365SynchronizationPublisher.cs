using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SynchronizationPublisher
{
    Task PublishAsync(
        Microsoft365SynchronizationWork work,
        CancellationToken cancellationToken = default);
}
