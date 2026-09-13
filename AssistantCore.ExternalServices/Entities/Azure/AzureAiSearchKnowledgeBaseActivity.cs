namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchKnowledgeBaseActivity(
    string Type,
    string? KnowledgeSourceName,
    string? Search,
    int? Count,
    int? ElapsedMilliseconds);
