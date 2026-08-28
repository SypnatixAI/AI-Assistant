using AssistantCore.Service.Application.Models.Conversations;

namespace AssistantCore.Service.Application.Services.Conversations;

public sealed record ConversationListingPage(
    IReadOnlyList<ConversationSummaryResponse> Items,
    bool HasMore);
