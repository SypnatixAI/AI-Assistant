namespace AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;

public sealed record AgenticRetrievalActivity(
    string Type,
    string? KnowledgeSourceName,
    string? Search,
    int? Count,
    int? ElapsedMilliseconds);
