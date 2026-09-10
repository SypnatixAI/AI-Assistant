namespace AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;

public sealed record AgenticRetrievalRequest(
    string Query,
    IReadOnlyCollection<AgenticRetrievalMessage> ConversationHistory,
    string KnowledgeBaseName,
    string KnowledgeSourceName,
    string Filter,
    int RetrievalCandidateLimit,
    int FinalEvidenceLimit,
    int MaxRuntimeInSeconds,
    int MaxOutputSizeInTokens);
