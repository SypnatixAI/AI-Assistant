using AssistantCore.RagEvaluation.Models;

namespace AssistantCore.RagEvaluation.Targets;

public interface IRagEvaluationTarget
{
    Task<EvaluationObservation> RunAsync(
        RagEvaluationCase evaluationCase,
        CancellationToken cancellationToken);
}
