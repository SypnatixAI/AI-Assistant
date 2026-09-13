using AssistantCore.RagEvaluation.Models;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class OfflineEvaluationTarget : IRagEvaluationTarget
{
    public Task<EvaluationObservation> RunAsync(
        RagEvaluationCase evaluationCase,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var retrieved = evaluationCase.Fixture.RetrievalRounds.SelectMany(round => round).ToArray();
        var cited = evaluationCase.Fixture.CitedSourceReferences;
        var hasInvalidCitation = cited.Any(reference => !retrieved.Contains(reference, StringComparer.Ordinal));
        var hasMissingCitation = retrieved.Length > 0 && cited.Count == 0;
        var outcome = hasInvalidCitation || hasMissingCitation
            ? EvaluationOutcome.Rejected
            : evaluationCase.Fixture.Outcome;
        IReadOnlyCollection<string> acceptedCitations = outcome == EvaluationOutcome.Rejected
            ? []
            : cited;

        return Task.FromResult(new EvaluationObservation(
            evaluationCase.Id,
            outcome,
            outcome == EvaluationOutcome.Rejected ? string.Empty : evaluationCase.Fixture.Answer,
            retrieved,
            acceptedCitations,
            evaluationCase.Fixture.SearchQueries,
            ModelCalls: 1,
            ToolCalls: evaluationCase.Fixture.RetrievalRounds.Count,
            DurationMilliseconds: 0));
    }
}
