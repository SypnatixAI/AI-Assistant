namespace AssistantCore.Service.Application.Models.Conversations;

public sealed record ConversationSummaryResponse(
    Guid Id,
    string Title,
    string Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? LastMessagePreview);
