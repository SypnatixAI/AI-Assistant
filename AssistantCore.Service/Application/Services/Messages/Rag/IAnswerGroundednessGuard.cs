using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public interface IAnswerGroundednessGuard
{
    const string ContentRejectedWarning = "rag.groundedness.content_rejected";

    int MaximumReformulationAttempts { get; }

    Task<MessageOrchestrationResult> ValidateAsync(MessageOrchestrationState state, MessageOrchestrationResult result, CancellationToken cancellationToken);
}
