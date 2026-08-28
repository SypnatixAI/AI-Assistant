using AssistantCore.Service.Application.Models.Conversations;

namespace AssistantCore.Service.Application.Services.Conversations;

public sealed record ConversationMessageListingPage(
    IReadOnlyList<ConversationMessageResponse> Items,
    string? NextCursor,
    bool HasMore);
