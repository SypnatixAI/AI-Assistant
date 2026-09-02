using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SendMessage;
using AssistantCore.Service.Application.Commands.SendMessage.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;

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

    /// <summary>
    /// Le flux emploie la meme convention de nommage que les reponses JSON ordinaires
    /// de l'API. Sans ces options, <c>JsonSerializer</c> conserverait les noms de
    /// proprietes C# et le flux serait le seul endroit de l'API en PascalCase.
    /// </summary>
    private static readonly JsonSerializerOptions StreamSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpPost("stream")]
    [Produces("text/event-stream")]
    [SwaggerOperation(Summary = "Envoyer un message et recevoir la réponse progressivement")]
    [SwaggerResponse(StatusCodes.Status200OK, "Server-Sent Events containing the assistant response.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid request.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    public async Task SendMessageStream(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        var events = await dispatcher.SendAsync(
            new SendMessageStreamCommand(
                request.ConversationId,
                request.Message,
                request.Model),
            cancellationToken);

        await foreach (var streamEvent in events.WithCancellation(cancellationToken))
        {
            var payload = JsonSerializer.Serialize(streamEvent.Data, StreamSerializerOptions);
            await Response.WriteAsync($"event: {streamEvent.Name}\ndata: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
