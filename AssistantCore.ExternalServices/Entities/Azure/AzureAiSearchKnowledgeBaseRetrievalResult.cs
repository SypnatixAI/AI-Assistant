namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchKnowledgeBaseRetrievalResult(
    string MergedContent,
    IReadOnlyCollection<AzureAiSearchKnowledgeBaseReference> References,
    IReadOnlyCollection<AzureAiSearchKnowledgeBaseActivity> Activity);
