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
}
