using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Services.Conversations.Pagination;

namespace AssistantCore.Service.Application.Services.Conversations;

public sealed class ConversationMessageListingService(
    IConversationRepository conversationRepository,
    IConversationMessageCursorCodec cursorCodec) : IConversationMessageListingService
{
    public async Task<ConversationMessageListingPage?> ListAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        int limit,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.FindConversationAsync(
            organizationId,
            ownerMemberId,
            conversationId,
            cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        var page = await conversationRepository.ListMessagesAsync(
            conversationId,
            limit,
            cursorCreatedAt,
            cursorId,
            cancellationToken);

        var items = page.Items
            .Select(item => new ConversationMessageResponse(
                item.Id,
                item.Role.ToString(),
                item.Content,
                item.ProcessingStatus.ToString(),
                item.Model,
                item.CreatedAt,
                item.UpdatedAt,
                item.Sources
                    .Select(source => new ConversationMessageSourceResponse(
                        source.SourceType,
                        source.Title,
                        source.Url,
                        source.Reference,
                        source.SourceDate))
                    .ToList()))
            .ToList();

        var nextCursor = page.HasMore
            ? cursorCodec.Encode(new ConversationMessageCursor(
                conversationId,
                page.NextCursorCreatedAt!.Value,
                page.NextCursorId!.Value))
            : null;

        return new ConversationMessageListingPage(items, nextCursor, page.HasMore);
    }
}
