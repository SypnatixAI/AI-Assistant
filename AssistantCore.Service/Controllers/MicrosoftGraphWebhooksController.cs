using System.Text;
using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.ReceiveMicrosoftGraphWebhook;
using AssistantCore.Service.Application.Models.Microsoft365;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistantCore.Service.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhooks/microsoft-graph")]
public sealed class MicrosoftGraphWebhooksController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveAsync(
        [FromQuery] string? validationToken,
        CancellationToken cancellationToken)
    {
        MicrosoftGraphNotificationCollection? notifications = null;
        if (validationToken is null)
        {
            notifications = await Request.ReadFromJsonAsync<MicrosoftGraphNotificationCollection>(
                cancellationToken);
        }

        var result = await dispatcher.SendAsync(
            new ReceiveMicrosoftGraphWebhookCommand(validationToken, notifications),
            cancellationToken);

        return result.ValidationToken is not null
            ? Content(result.ValidationToken, "text/plain", Encoding.UTF8)
            : Accepted();
    }
}
