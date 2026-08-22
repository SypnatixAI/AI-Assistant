using AssistantCore.Service.Application.Commands.ReceiveMicrosoftGraphWebhook;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AssistantCore.Service.Tests.Controllers;

public sealed class MicrosoftGraphWebhooksControllerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AValidationToken_When_ReceiveAsync_Then_ReturnsExactPlainText(
        string validationToken,
        CancellationToken cancellationToken)
    {
        // Given
        var dispatcher = new RecordingDispatcher
        {
            Response = new ReceiveMicrosoftGraphWebhookResult(validationToken)
        };
        var controller = new MicrosoftGraphWebhooksController(dispatcher);

        // When
        var result = await controller.ReceiveAsync(
            validationToken,
            cancellationToken);

        // Then
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(200, content.StatusCode ?? 200);
        Assert.Equal("text/plain; charset=utf-8", content.ContentType);
        Assert.Equal(validationToken, content.Content);
        var command = Assert.IsType<ReceiveMicrosoftGraphWebhookCommand>(dispatcher.ReceivedRequest);
        Assert.Equal(validationToken, command.ValidationToken);
    }

    [Theory, AutoDomainData]
    public async Task Given_NormalNotification_When_ReceiveAsync_Then_ReturnsAccepted(
        MicrosoftGraphNotification notification,
        CancellationToken cancellationToken)
    {
        // Given
        var notifications = new MicrosoftGraphNotificationCollection([notification]);
        var dispatcher = new RecordingDispatcher
        {
            Response = new ReceiveMicrosoftGraphWebhookResult(ValidationToken: null)
        };
        var controller = new MicrosoftGraphWebhooksController(dispatcher);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Body = new MemoryStream(
            JsonSerializer.SerializeToUtf8Bytes(notifications));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // When
        var result = await controller.ReceiveAsync(
            validationToken: null,
            cancellationToken: cancellationToken);

        // Then
        Assert.IsType<AcceptedResult>(result);
        var command = Assert.IsType<ReceiveMicrosoftGraphWebhookCommand>(dispatcher.ReceivedRequest);
        Assert.Same(notifications, command.Notifications);
    }
}
