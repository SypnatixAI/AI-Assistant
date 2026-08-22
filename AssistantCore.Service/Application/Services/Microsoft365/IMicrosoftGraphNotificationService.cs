using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoftGraphNotificationService
{
    Task HandleNotificationsAsync(
        IReadOnlyCollection<MicrosoftGraphNotification> notifications,
        CancellationToken cancellationToken = default);
}
