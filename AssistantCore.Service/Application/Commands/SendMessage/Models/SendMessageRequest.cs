using System.ComponentModel.DataAnnotations;

namespace AssistantCore.Service.Application.Commands.SendMessage.Models;

public sealed record SendMessageRequest(
    Guid? ConversationId,
    [property: Required] string Message,
    string? Model);
