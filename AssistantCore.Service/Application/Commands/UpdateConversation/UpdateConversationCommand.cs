using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Conversations;

namespace AssistantCore.Service.Application.Commands.UpdateConversation;

public sealed record UpdateConversationCommand(
    Guid ConversationId,
    string? Title,
    string? Status,
    int? ExpectedVersion) : IRequest<ConversationResponse>;
