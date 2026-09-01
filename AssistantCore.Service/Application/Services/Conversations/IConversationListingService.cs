using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Services.Conversations;

public interface IConversationListingService
{
    Task<ConversationListingPage> ListAsync(
        Guid organizationId,
        Guid ownerMemberId,
        ConversationStatus status,
        int limit,
        DateTimeOffset? cursorUpdatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken);
}
