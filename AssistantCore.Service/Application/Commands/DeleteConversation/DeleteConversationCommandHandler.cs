using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.DeleteConversation.Models;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Application.Commands.DeleteConversation;

public sealed class DeleteConversationCommandHandler(
    IMessageUserContextService userContextService,
    IConversationLifecycleService conversationLifecycleService)
    : IRequestHandler<DeleteConversationCommand, DeleteConversationResponse>
{
    public async Task<DeleteConversationResponse> HandleAsync(
        DeleteConversationCommand request,
        CancellationToken cancellationToken)
    {
        var userContext = await userContextService.GetCurrentAsync(cancellationToken);

        if (request.ConversationId == Guid.Empty)
        {
            throw new BadRequestException("conversationId is invalid.");
        }

        var alreadyDeleted = await conversationLifecycleService.DeleteAsync(
            userContext.Organization.Id,
            userContext.Member.Id,
            request.ConversationId,
            cancellationToken);

        return new DeleteConversationResponse(request.ConversationId, alreadyDeleted);
    }
}
