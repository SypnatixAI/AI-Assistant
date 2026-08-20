using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365IngestionOrchestrator(
    IMicrosoft365ConnectionRepository connectionRepository)
    : IMicrosoft365IngestionOrchestrator
{
    public async Task ScheduleInitialSynchronizationAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionRepository.FindForProcessingAsync(
            connectionId,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 connection was not found.");

        if (connection.Status != Microsoft365ConnectionStatus.Active)
        {
            throw new InvalidOperationException(
                $"Microsoft 365 connection {connectionId:D} cannot be processed while its status is {connection.Status}.");
        }
    }
}
