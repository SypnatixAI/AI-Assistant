namespace AssistantCore.Service.Application.Models.Conversations;

public sealed record ConversationMessageResponse(
    Guid Id,
    string Role,
    string Content,
    string ProcessingStatus,
    string? Model,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<ConversationMessageSourceResponse> Sources);
