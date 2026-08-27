namespace AssistantCore.Service.Application.Models.Conversations;

public sealed record ListConversationsResponse(
    IReadOnlyCollection<ConversationSummaryResponse> Conversations,
    string? NextCursor,
    bool HasMore);
