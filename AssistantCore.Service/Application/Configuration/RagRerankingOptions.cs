namespace AssistantCore.Service.Application.Configuration;

public sealed class RagRerankingOptions
{
    public bool DedicatedCrossEncoderEnabled { get; init; }
    public decimal MaximumEstimatedCost { get; init; } = 0.01m;
    public double AmbiguityScoreGap { get; init; } = 0.15;
}
