namespace AssistantCore.Service.Application.Models.Conversations;

public sealed record ConversationResponse(
    Guid Id,
    string Title,
    string Status,
    DateTimeOffset UpdatedAt,
    int Version);
