namespace AssistantCore.Service.Application.Configuration;

public sealed class RagOptions
{
    public const string SectionName = "Rag";
    public VectorSearchOptions VectorSearch { get; init; } = new();
    public CorrectiveRagOptions CorrectiveRag { get; init; } = new();
    public RagRerankingOptions Reranking { get; init; } = new();
    public RagDiversificationOptions Diversification { get; init; } = new();

    public bool IsValid() => VectorSearch.Metric == "cosine"
        && CorrectiveRag.MaximumCorrectionAttempts is >= 0 and <= 5
        && CorrectiveRag.RetrievalConfidenceThreshold is > 0 and <= 1
        && CorrectiveRag.GroundednessConfidenceThreshold is > 0 and <= 1
        && CorrectiveRag.MaximumGroundednessReformulationAttempts is >= 0 and <= 5
        && CorrectiveRag.MinimumSemanticScore is >= 0 and <= 4
        && CorrectiveRag.MinimumRelevantPassages > 0
        && CorrectiveRag.MinimumDistinctSources > 0
        && CorrectiveRag.MaximumStageDurationSeconds is > 0 and <= 60
        && CorrectiveRag.EstimatedSearchCost >= 0
        && Reranking.MaximumEstimatedCost >= 0
        && Reranking.AmbiguityScoreGap is >= 0 and <= 4
        && Diversification.MaximumConsecutiveFromSameSource > 0
        && Diversification.MaximumPromotionScoreGap is >= 0 and <= 4
        && Diversification.NearDuplicateOverlapThreshold is > 0 and <= 1;
}
