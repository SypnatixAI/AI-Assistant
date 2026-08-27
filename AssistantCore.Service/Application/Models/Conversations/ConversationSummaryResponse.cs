namespace AssistantCore.Service.Application.Models.Conversations;

public sealed record ConversationSummaryResponse(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? LastMessagePreview);
