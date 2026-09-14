using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Repository.Repositories;

public interface IMessageCompletionRepository
{
    Task<Message?> CompleteWithUsageAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid userMessageId,
        Message assistantMessage,
        IReadOnlyCollection<MessageSource> sources,
        IReadOnlyCollection<MessageWarning> warnings,
        TokenConsumption consumption,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);
}
