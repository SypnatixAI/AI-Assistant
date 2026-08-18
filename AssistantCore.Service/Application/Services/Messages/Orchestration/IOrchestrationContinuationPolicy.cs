using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IOrchestrationContinuationPolicy
{
    OrchestrationContinuationDecision Evaluate(
        MessageOrchestrationState state,
        AiModelDecision decision);
}
