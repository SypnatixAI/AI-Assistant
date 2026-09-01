using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Conversations;

namespace AssistantCore.Service.Application.Commands.ListConversations;

public sealed record ListConversationsCommand(
    int? Limit,
    string? Cursor,
    string? Status) : IRequest<ListConversationsResponse>;
