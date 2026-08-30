using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Application.Commands.UpdateConversation;

public sealed class UpdateConversationCommandHandler(
    IMessageUserContextService userContextService,
    IConversationLifecycleService conversationLifecycleService)
    : IRequestHandler<UpdateConversationCommand, ConversationResponse>
{
    public async Task<ConversationResponse> HandleAsync(
        UpdateConversationCommand request,
        CancellationToken cancellationToken)
    {
        var userContext = await userContextService.GetCurrentAsync(cancellationToken);

        if (request.ConversationId == Guid.Empty)
        {
            throw new BadRequestException("conversationId is invalid.");
        }

        return await conversationLifecycleService.UpdateAsync(
            userContext.Organization.Id,
            userContext.Member.Id,
            request.ConversationId,
            request.Title,
            request.Status,
            request.ExpectedVersion,
            cancellationToken);
    }
}
