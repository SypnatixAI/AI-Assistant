namespace AssistantCore.Service.Application.Configuration;

public sealed class AzureAiSearchOptions
{
    public const string SectionName = "AzureSearch";
    public string Endpoint { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
    public bool EnsureIndexOnStartup { get; init; }
    public bool SemanticRankingEnabled { get; init; } = true;
    public string SemanticConfigurationName { get; init; } = "m365-semantic";
    public string VectorSearchMetric { get; init; } = "cosine";
    public double MinimumSemanticRelevanceScore { get; init; } = 1.5d;
    public string KnowledgeSourceName { get; init; } = "synaptix-m365-knowledge-source";
    public string KnowledgeBaseName { get; init; } = "synaptix-m365-knowledge-base";
    public string KnowledgeBaseRetrievalReasoningEffort { get; init; } = "minimal";
    public string VectorizerName { get; init; } = "m365-azure-openai-vectorizer";
    public string PlanningModelEndpoint { get; init; } = string.Empty;
    public string PlanningModelDeploymentName { get; init; } = string.Empty;
    public string PlanningModelName { get; init; } = string.Empty;
    public string PlanningModelApiKey { get; init; } = string.Empty;
    public int KnowledgeBaseMaxRuntimeInSeconds { get; init; } = 30;
    public int? KnowledgeBaseMaxOutputSizeInTokens { get; init; }
}
