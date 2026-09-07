using AssistantCore.RagEvaluation.Models;
using AssistantCore.Service.Application.Models.Messages.Rag;
using AssistantCore.Service.Application.Services.Messages.Rag;

namespace AssistantCore.RagEvaluation.Scoring;

public sealed record AdaptiveRagComparisonMetrics(
    int K, double RecallAtK, double PrecisionAtK, double Mrr,
    double ExtractiveGroundedness, double LabeledHallucinationRate,
    double InsufficientlySourcedAnswerRate, double LatencyP50Ms, double LatencyP95Ms,
    decimal AverageEstimatedCost, double AverageCorrections, double AnswerRate, double AbstentionRate, double ErrorRate)
{
    public static async Task<AdaptiveRagComparisonMetrics> CalculateAsync(EvaluationDataset dataset,
        IReadOnlyCollection<EvaluationObservation> observations, int k, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);
        var recall = new List<double>();
        var precision = new List<double>();
        var ranks = new List<double>();
        var grounding = new List<double>();
        var hallucinations = 0;
        var unsourced = 0;
        var answers = 0;
        foreach (var observation in observations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scenario = dataset.Cases.Single(c => c.Id == observation.CaseId);
            var expected = scenario.Expected.ExpectedSourceReferences.ToHashSet(StringComparer.Ordinal);
            var retrieved = observation.RetrievedSourceReferences.Distinct().Take(k).ToArray();
            var relevant = retrieved.Count(expected.Contains);
            recall.Add(expected.Count == 0 ? 1 : (double)relevant / expected.Count);
            precision.Add(retrieved.Length == 0 ? (expected.Count == 0 ? 1 : 0) : (double)relevant / retrieved.Length);
            var rank = Array.FindIndex(retrieved, expected.Contains);
            ranks.Add(rank < 0 ? 0 : 1d / (rank + 1));
            if (observation.Outcome != EvaluationOutcome.Answer) continue;
            answers++;
            var passages = scenario.Documents.Where(d => d.Allowed && observation.CitedSourceReferences.Contains(d.Reference))
                .Select(d => new RagPassage(d.Reference, d.Content, d.Reference)).ToArray();
            var check = await new ExtractiveAnswerGroundednessEvaluator().EvaluateAsync(observation.Answer, passages, cancellationToken);
            grounding.Add(check.Confidence);
            if (passages.Length == 0 || !check.Passed) unsourced++;
            if (scenario.Expected.ForbiddenAnswerTerms.Any(term => observation.Answer.Contains(term, StringComparison.OrdinalIgnoreCase))) hallucinations++;
        }
        var latencies = observations.Select(o => (double)o.DurationMilliseconds).Order().ToArray();
        return new(k, Average(recall), Average(precision), Average(ranks), Average(grounding),
            answers == 0 ? 0 : (double)hallucinations / answers,
            answers == 0 ? 0 : (double)unsourced / answers,
            Percentile(latencies, 0.5), Percentile(latencies, 0.95),
            observations.Count == 0 ? 0 : observations.Average(o => o.EstimatedCost),
            observations.Count == 0 ? 0 : observations.Average(o => o.CorrectionAttempts),
            observations.Count == 0 ? 0 : (double)answers / observations.Count,
            observations.Count == 0 ? 0 : (double)observations.Count(o => o.Outcome == EvaluationOutcome.CannotAnswer) / observations.Count,
            observations.Count == 0 ? 0 : (double)observations.Count(o => o.Outcome is EvaluationOutcome.Error or EvaluationOutcome.Rejected) / observations.Count);
    }

    private static double Average(List<double> values) => values.Count == 0 ? 0 : values.Average();
    private static double Percentile(double[] values, double percentile) => values.Length == 0 ? 0 : values[(int)Math.Ceiling(values.Length * percentile) - 1];
}
