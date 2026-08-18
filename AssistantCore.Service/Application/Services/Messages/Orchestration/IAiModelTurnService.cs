using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IAiModelTurnService
{
    Task<AiModelResponse> RequestNextActionAsync(
        MessageOrchestrationState state,
        CancellationToken cancellationToken);
}
