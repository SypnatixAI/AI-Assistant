using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public sealed record ConversationListItem(
    Guid Id,
    string Title,
    ConversationStatus Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? LastMessageContent);

public sealed record ConversationListPage(
    IReadOnlyList<ConversationListItem> Items,
    bool HasMore);
