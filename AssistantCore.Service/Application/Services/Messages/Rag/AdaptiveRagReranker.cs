using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Rag;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public sealed class AdaptiveRagReranker(
    IRagReranker baseline,
    IOptions<RagOptions> options,
    TimeProvider timeProvider,
    IDedicatedRagReranker? dedicated = null)
{
    public async Task<IReadOnlyList<RagPassage>> RerankAsync(string query, IReadOnlyList<RagPassage> passages,
        RetrievalQualityResult quality, ConnectorExecutionContext context, CancellationToken cancellationToken)
    {
        var settings = options.Value.Reranking;
        var scores = passages.Where(p => p.SemanticScore.HasValue).Select(p => p.SemanticScore!.Value).OrderDescending().Take(2).ToArray();
        var ambiguous = scores.Length == 2 && scores[0] - scores[1] <= settings.AmbiguityScoreGap;
        var cost = settings.DedicatedCrossEncoderEnabled && dedicated is not null
            ? dedicated.EstimateCost(passages) : decimal.MaxValue;
        var useDedicated = settings.DedicatedCrossEncoderEnabled && dedicated is not null
            && passages.Count > 0 && (!quality.IsSufficient || ambiguous)
            && cost >= 0 && cost <= settings.MaximumEstimatedCost
            && context.Budget?.TryReserveAuxiliaryOperation(cost, timeProvider.GetUtcNow()) == true;
        using var activity = RagTelemetry.Activities.StartActivity("rag.reranking");
        activity?.SetTag("rag.reranker.type", useDedicated ? "dedicated" : "semantic-only");
        var started = timeProvider.GetTimestamp();
        try
        {
            var result = await (useDedicated ? dedicated! : baseline).RerankAsync(query, passages, cancellationToken).WaitAsync(cancellationToken);
            // A reranker may reorder or remove candidates, never introduce or alter evidence.
            var originals = passages.ToDictionary(p => p.Reference, StringComparer.Ordinal);
            if (result.Any(p => !originals.TryGetValue(p.Reference, out var original) || p != original)
                || result.Select(p => p.Reference).Distinct().Count() != result.Count)
                throw new InvalidOperationException("Reranker returned evidence outside its input.");
            if (useDedicated) RagTelemetry.Record("rag.corrective.estimated_cost", (double)cost);
            return result;
        }
        finally { RagTelemetry.Record("rag.reranker.duration_ms", timeProvider.GetElapsedTime(started).TotalMilliseconds); }
    }
}
