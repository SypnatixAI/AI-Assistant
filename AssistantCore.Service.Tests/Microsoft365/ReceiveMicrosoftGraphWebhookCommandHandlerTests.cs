using AssistantCore.Service.Application.Commands.ReceiveMicrosoftGraphWebhook;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class ReceiveMicrosoftGraphWebhookCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AValidationToken_When_HandleAsync_Then_TokenIsReturnedWithoutNotificationProcessing(
        string validationToken)
    {
        // Given
        var service = new NotificationServiceFake();
        var handler = new ReceiveMicrosoftGraphWebhookCommandHandler(service);

        // When
        var result = await handler.HandleAsync(
            new ReceiveMicrosoftGraphWebhookCommand(validationToken, Notifications: null),
            CancellationToken.None);

        // Then
        Assert.Equal(validationToken, result.ValidationToken);
        Assert.Equal(0, service.CallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_NormalNotifications_When_HandleAsync_Then_NotificationsAreDelegated(
        MicrosoftGraphNotification notification)
    {
        // Given
        var service = new NotificationServiceFake();
        var handler = new ReceiveMicrosoftGraphWebhookCommandHandler(service);

        // When
        var result = await handler.HandleAsync(
            new ReceiveMicrosoftGraphWebhookCommand(
                ValidationToken: null,
                Notifications: new MicrosoftGraphNotificationCollection([notification])),
            CancellationToken.None);

        // Then
        Assert.Null(result.ValidationToken);
        Assert.Equal(1, service.CallCount);
        Assert.Same(notification, Assert.Single(service.Notifications!));
    }

    private sealed class NotificationServiceFake : IMicrosoftGraphNotificationService
    {
        public int CallCount { get; private set; }

        public IReadOnlyCollection<MicrosoftGraphNotification>? Notifications { get; private set; }

        public Task HandleNotificationsAsync(
            IReadOnlyCollection<MicrosoftGraphNotification> notifications,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Notifications = notifications;
            return Task.CompletedTask;
        }
    }
}
