using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Orchestration;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class RecordingAiModelTurnService(IAiModelTurnService inner)
    : IAiModelTurnService
{
    private readonly List<AiModelDecision> _decisions = [];

    public IReadOnlyCollection<AiModelDecision> Decisions => _decisions;

    public async Task<AiModelResponse> RequestNextActionAsync(
        MessageOrchestrationState state,
        CancellationToken cancellationToken)
    {
        var response = await inner.RequestNextActionAsync(state, cancellationToken);
        _decisions.Add(response.Decision);
        return response;
    }

    public async Task<AiModelResponse> RequestNextActionStreamingAsync(
        MessageOrchestrationState state,
        Func<string, CancellationToken, ValueTask> onAnswerDelta,
        CancellationToken cancellationToken)
    {
        var response = await inner.RequestNextActionStreamingAsync(
            state,
            onAnswerDelta,
            cancellationToken);
        _decisions.Add(response.Decision);
        return response;
    }
}
