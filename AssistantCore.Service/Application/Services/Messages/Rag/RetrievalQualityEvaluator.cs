using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Rag;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public sealed class RetrievalQualityEvaluator(IOptions<RagOptions> options) : IRetrievalQualityEvaluator
{
    public Task<RetrievalQualityResult> EvaluateAsync(string query, IReadOnlyCollection<RagPassage> passages, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = options.Value.CorrectiveRag;
        var relevant = passages.Where(p => !string.IsNullOrWhiteSpace(p.Content)
            && p.SemanticScore is double score && double.IsFinite(score)
            && score >= settings.MinimumSemanticScore).ToArray();
        var confidence = relevant.Length == 0 ? 0 :
            Math.Clamp(relevant.Max(p => p.SemanticScore!.Value) / 4, 0, 1)
            * Math.Min(1d, (double)relevant.Length / settings.MinimumRelevantPassages)
            * Math.Min(1d, (double)relevant.Select(p => p.SourceIdentity).Distinct().Count() / settings.MinimumDistinctSources);
        return Task.FromResult(new RetrievalQualityResult(
            confidence >= settings.RetrievalConfidenceThreshold, confidence,
            relevant.Length == 0 ? "No nonempty passage with sufficient semantic relevance." : "Semantic relevance, passage count and source diversity."));
    }
}
