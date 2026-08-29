namespace AssistantCore.Service.Application.Models.Conversations;

public sealed record ConversationMessageSourceResponse(
    string Type,
    string Title,
    string? Url,
    string Reference,
    DateTimeOffset? SourceDate);
