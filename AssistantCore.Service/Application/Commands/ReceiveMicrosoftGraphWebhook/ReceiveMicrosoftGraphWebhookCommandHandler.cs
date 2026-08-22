using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.ReceiveMicrosoftGraphWebhook;

public sealed class ReceiveMicrosoftGraphWebhookCommandHandler(
    IMicrosoftGraphNotificationService notificationService)
    : IRequestHandler<ReceiveMicrosoftGraphWebhookCommand, ReceiveMicrosoftGraphWebhookResult>
{
    public async Task<ReceiveMicrosoftGraphWebhookResult> HandleAsync(
        ReceiveMicrosoftGraphWebhookCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ValidationToken is not null)
        {
            return new ReceiveMicrosoftGraphWebhookResult(request.ValidationToken);
        }

        await notificationService.HandleNotificationsAsync(
            request.Notifications?.Value ?? [],
            cancellationToken);
        return new ReceiveMicrosoftGraphWebhookResult(ValidationToken: null);
    }
}
