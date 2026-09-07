using AssistantCore.RagEvaluation.Models;
using AssistantCore.RagEvaluation.Scoring;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Rag;

namespace AssistantCore.RagEvaluation.Targets;

public sealed record AdaptiveVariantReport(string Variant, string Status,
    AdaptiveRagComparisonMetrics? Metrics, IReadOnlyCollection<EvaluationObservation> Observations);

public sealed class AdaptiveRagComparisonRunner
{
    public async Task<IReadOnlyList<AdaptiveVariantReport>> RunAsync(EvaluationDataset dataset, string model,
        Func<RagEvaluationCase, IReadOnlyDictionary<string, RetrievedEvidence>, IAiModelProvider> providerFactory,
        IDedicatedRagReranker? dedicatedReranker = null, CancellationToken cancellationToken = default)
    {
        var reports = new List<AdaptiveVariantReport>();
        foreach (var variant in new[] { "A", "B", "C", "D" })
        {
            var dedicated = variant is "C" or "D";
            if (dedicated && dedicatedReranker is null)
            {
                reports.Add(new(variant, "unavailable: no dedicated reranker supplied", null, []));
                continue;
            }
            var options = new RagOptions
            {
                CorrectiveRag = new() { Enabled = variant is "B" or "D" },
                Reranking = new() { DedicatedCrossEncoderEnabled = dedicated }
            };
            var target = new OrchestrationEvaluationTarget(providerFactory, TimeProvider.System, options, dedicatedReranker);
            var observations = new List<EvaluationObservation>();
            foreach (var scenario in dataset.Cases)
                observations.Add(await target.RunAsync(scenario, model, cancellationToken));
            reports.Add(new(variant, "completed", await AdaptiveRagComparisonMetrics.CalculateAsync(dataset, observations, 10, cancellationToken), observations));
        }
        return reports;
    }
}
