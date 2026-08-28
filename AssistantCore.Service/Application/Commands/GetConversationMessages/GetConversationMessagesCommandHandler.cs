using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Conversations.Pagination;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Application.Commands.GetConversationMessages;

public sealed class GetConversationMessagesCommandHandler(
    IMessageUserContextService userContextService,
    IConversationMessageListingService conversationMessageListingService,
    IConversationMessageCursorCodec cursorCodec)
    : IRequestHandler<GetConversationMessagesCommand, GetConversationMessagesResponse>
{
    public async Task<GetConversationMessagesResponse> HandleAsync(
        GetConversationMessagesCommand request,
        CancellationToken cancellationToken)
    {
        var userContext = await userContextService.GetCurrentAsync(cancellationToken);

        if (request.ConversationId == Guid.Empty)
        {
            throw new BadRequestException("conversationId is invalid.");
        }

        var limit = ConversationMessageListingLimits.Validate(request.Limit);
        var cursor = cursorCodec.Decode(request.Cursor, request.ConversationId);

        var page = await conversationMessageListingService.ListAsync(
            userContext.Organization.Id,
            userContext.Member.Id,
            request.ConversationId,
            limit,
            cursor?.CreatedAt,
            cursor?.Id,
            cancellationToken)
            ?? throw new NotFoundException("Conversation not found.");

        return new GetConversationMessagesResponse(
            request.ConversationId,
            page.Items,
            page.NextCursor,
            page.HasMore);
    }
}
