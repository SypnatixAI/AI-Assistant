using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.DeleteConversation.Models;

namespace AssistantCore.Service.Application.Commands.DeleteConversation;

public sealed record DeleteConversationCommand(
    Guid ConversationId) : IRequest<DeleteConversationResponse>;
