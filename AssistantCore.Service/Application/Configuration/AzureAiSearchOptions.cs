namespace AssistantCore.Service.Application.Configuration;

public sealed class AzureAiSearchOptions
{
    public const string SectionName = "AzureSearch";
    public string Endpoint { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
}
