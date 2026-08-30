using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public enum ConversationUpdateStatus
{
    Updated,
    NotFound,
    VersionConflict
}

public enum ConversationDeleteStatus
{
    Deleted,
    AlreadyDeleted,
    NotFound
}

public sealed record ConversationUpdateResult(
    ConversationUpdateStatus Status,
    Conversation? Conversation)
{
    public static ConversationUpdateResult NotFound { get; } =
        new(ConversationUpdateStatus.NotFound, null);

    public static ConversationUpdateResult VersionConflict { get; } =
        new(ConversationUpdateStatus.VersionConflict, null);

    public static ConversationUpdateResult Updated(Conversation conversation) =>
        new(ConversationUpdateStatus.Updated, conversation);
}
