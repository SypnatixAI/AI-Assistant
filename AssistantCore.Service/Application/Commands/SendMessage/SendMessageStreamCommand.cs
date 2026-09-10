using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage.Models;

namespace AssistantCore.Service.Application.Commands.SendMessage;

public sealed record SendMessageStreamCommand(
    Guid? ConversationId,
    string Message) : IRequest<IAsyncEnumerable<SendMessageStreamEvent>>;
