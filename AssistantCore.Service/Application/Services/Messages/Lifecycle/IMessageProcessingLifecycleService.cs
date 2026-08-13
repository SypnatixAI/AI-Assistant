using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Lifecycle;

public interface IMessageProcessingLifecycleService
{
    Task<StartedMessageProcessing> StartAsync(
        Guid? conversationId,
        string message,
        CancellationToken cancellationToken);

    Task MarkAsInProgressAsync(
        StartedMessageProcessing processing,
        CancellationToken cancellationToken);

    Task<CompletedMessageProcessing> CompleteAsync(
        StartedMessageProcessing processing,
        MessageOrchestrationResult result,
        CancellationToken cancellationToken);

    Task FailAsync(
        StartedMessageProcessing processing,
        MessageProcessingFailure failure,
        CancellationToken cancellationToken);
}
