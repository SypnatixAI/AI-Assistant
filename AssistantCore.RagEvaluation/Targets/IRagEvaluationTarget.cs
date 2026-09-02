using AssistantCore.RagEvaluation.Models;

namespace AssistantCore.RagEvaluation.Targets;

public interface IRagEvaluationTarget
{
    Task<EvaluationObservation> RunAsync(
        RagEvaluationCase evaluationCase,
        string model,
        CancellationToken cancellationToken);
}
