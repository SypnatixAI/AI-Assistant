namespace AssistantCore.Service.Application.Configuration;

public sealed class CorrectiveRagOptions
{
    public bool Enabled { get; init; }
    public int MaximumCorrectionAttempts { get; init; } = 1;
    public double RetrievalConfidenceThreshold { get; init; } = 0.65;
    public double MinimumSemanticScore { get; init; } = 2;
    public int MinimumRelevantPassages { get; init; } = 1;
    public int MinimumDistinctSources { get; init; } = 1;
    public bool GroundednessCheckEnabled { get; init; } = true;
    public double GroundednessConfidenceThreshold { get; init; } = 0.75;
    public int MaximumGroundednessReformulationAttempts { get; init; } = 3;
    public int MaximumStageDurationSeconds { get; init; } = 5;
    // Deployment-specific estimate, charged before each additional search.
    public decimal EstimatedSearchCost { get; init; } = 0.001m;
}
