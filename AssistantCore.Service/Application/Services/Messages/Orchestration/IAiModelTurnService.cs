using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IAiModelTurnService
{
    Task<AiModelResponse> RequestNextActionAsync(
        MessageOrchestrationState state,
        CancellationToken cancellationToken);

    Task<AiModelResponse> RequestNextActionStreamingAsync(
        MessageOrchestrationState state,
        Func<string, CancellationToken, ValueTask> onAnswerDelta,
        CancellationToken cancellationToken);
}
