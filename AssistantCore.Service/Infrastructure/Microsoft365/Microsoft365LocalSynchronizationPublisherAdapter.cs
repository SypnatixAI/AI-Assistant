using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365LocalSynchronizationPublisherAdapter
    : IMicrosoft365SynchronizationPublisher
{
    public Task PublishAsync(
        Microsoft365SynchronizationWork work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        cancellationToken.ThrowIfCancellationRequested();

        // The synchronization is already stored in SQL and claimed by the local worker.
        return Task.CompletedTask;
    }
}
