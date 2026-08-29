using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetConversationMessages;
using AssistantCore.Service.Application.Commands.ListConversations;
using AssistantCore.Service.Application.Models.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/conversations")]
public sealed class ConversationsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Lister les conversations de l'utilisateur courant")]
    [SwaggerResponse(StatusCodes.Status200OK, "Conversations returned successfully.", typeof(ListConversationsResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid pagination parameters.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Organization or member access denied.")]
    public async Task<ActionResult<ListConversationsResponse>> ListConversations(
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new ListConversationsCommand(limit, cursor),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{conversationId}/messages")]
    [SwaggerOperation(Summary = "Charger l'historique des messages d'une conversation")]
    [SwaggerResponse(StatusCodes.Status200OK, "Messages returned successfully.", typeof(GetConversationMessagesResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid conversationId or pagination parameters.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Organization or member access denied.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Conversation not found.")]
    public async Task<ActionResult<GetConversationMessagesResponse>> GetMessages(
        Guid conversationId,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new GetConversationMessagesCommand(conversationId, limit, cursor),
            cancellationToken);

        return Ok(result);
    }
}
