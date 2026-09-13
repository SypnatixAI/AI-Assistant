namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchKnowledgeBaseMessage(
    string Role,
    string Content);
