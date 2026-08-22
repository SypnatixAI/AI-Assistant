using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.ReceiveMicrosoftGraphWebhook;

public sealed record ReceiveMicrosoftGraphWebhookCommand(
    string? ValidationToken,
    MicrosoftGraphNotificationCollection? Notifications)
    : IRequest<ReceiveMicrosoftGraphWebhookResult>;
