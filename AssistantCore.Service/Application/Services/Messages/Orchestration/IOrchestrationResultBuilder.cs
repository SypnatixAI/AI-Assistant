using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IOrchestrationResultBuilder
{
    MessageOrchestrationResult Build(
        MessageOrchestrationState state,
        AiModelResponse finalResponse);
}
