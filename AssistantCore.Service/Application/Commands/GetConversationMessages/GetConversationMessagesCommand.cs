using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Conversations;

namespace AssistantCore.Service.Application.Commands.GetConversationMessages;

public sealed record GetConversationMessagesCommand(
    Guid ConversationId,
    int? Limit,
    string? Cursor) : IRequest<GetConversationMessagesResponse>;
