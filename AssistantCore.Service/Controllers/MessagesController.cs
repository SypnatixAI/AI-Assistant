using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/messages")]
public sealed class MessagesController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(Summary = "Envoyer un message a l'assistant")]
    [SwaggerResponse(StatusCodes.Status200OK, "Message handled successfully.", typeof(SendMessageResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    public async Task<ActionResult<SendMessageResponse>> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new SendMessageCommand(
                request.ConversationId,
                request.Message,
                request.Model),
            cancellationToken);

        return Ok(result);
    }
}
