namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchKnowledgeBaseRetrievalRequest(
    string KnowledgeBaseName,
    string KnowledgeSourceName,
    string Query,
    IReadOnlyCollection<AzureAiSearchKnowledgeBaseMessage> ConversationHistory,
    string Filter,
    int RetrievalCandidateLimit,
    int FinalEvidenceLimit,
    int MaxRuntimeInSeconds,
    int MaxOutputSizeInTokens,
    string RetrievalReasoningEffort);
