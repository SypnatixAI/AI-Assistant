using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Conversations.Pagination;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Application.Commands.ListConversations;

public sealed class ListConversationsCommandHandler(
    IMessageUserContextService userContextService,
    IConversationListingService conversationListingService,
    IConversationCursorCodec cursorCodec)
    : IRequestHandler<ListConversationsCommand, ListConversationsResponse>
{
    public async Task<ListConversationsResponse> HandleAsync(
        ListConversationsCommand request,
        CancellationToken cancellationToken)
    {
        var userContext = await userContextService.GetCurrentAsync(cancellationToken);
        var limit = ConversationListingLimits.Validate(request.Limit);
        var status = ConversationStatusParser.ParseOrDefault(request.Status);
        var cursor = cursorCodec.Decode(request.Cursor);

        var page = await conversationListingService.ListAsync(
            userContext.Organization.Id,
            userContext.Member.Id,
            status,
            limit,
            cursor?.UpdatedAt,
            cursor?.Id,
            cancellationToken);

        var nextCursor = page.HasMore && page.Items.Count > 0
            ? cursorCodec.Encode(new ConversationCursor(
                page.Items[^1].UpdatedAt,
                page.Items[^1].Id))
            : null;

        return new ListConversationsResponse(page.Items, nextCursor, page.HasMore);
    }
}
