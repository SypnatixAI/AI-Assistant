using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.DeleteConversation;
using AssistantCore.Service.Application.Commands.GetConversationMessages;
using AssistantCore.Service.Application.Commands.ListConversations;
using AssistantCore.Service.Application.Commands.UpdateConversation;
using AssistantCore.Service.Application.Commands.UpdateConversation.Models;
using AssistantCore.Service.Application.Exceptions;
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
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid pagination parameters or status.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Organization or member access denied.")]
    public async Task<ActionResult<ListConversationsResponse>> ListConversations(
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new ListConversationsCommand(limit, cursor, status),
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

    [HttpPatch("{conversationId}")]
    [SwaggerOperation(Summary = "Renommer, archiver ou restaurer une conversation")]
    [SwaggerResponse(StatusCodes.Status200OK, "Conversation updated successfully.", typeof(ConversationResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid conversationId, title or status.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Organization or member access denied.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Conversation not found.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The conversation was modified concurrently.")]
    public async Task<ActionResult<ConversationResponse>> UpdateConversation(
        Guid conversationId,
        [FromBody] UpdateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new UpdateConversationCommand(
                conversationId,
                request.Title,
                request.Status,
                ReadExpectedVersion()),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{conversationId}")]
    [SwaggerOperation(Summary = "Supprimer une conversation et programmer sa purge")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Conversation deleted successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid conversationId.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Organization or member access denied.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Conversation not found.")]
    public async Task<IActionResult> DeleteConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(
            new DeleteConversationCommand(conversationId),
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Lit la version attendue depuis l'en-tete <c>If-Match</c>. L'en-tete est optionnel :
    /// absent, aucune verification de concurrence n'est demandee. Present mais illisible,
    /// la demande est refusee plutot que d'ecraser silencieusement une version plus recente.
    /// </summary>
    private int? ReadExpectedVersion()
    {
        var header = Request.Headers.IfMatch.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var candidate = header.Trim();

        if (candidate.StartsWith("W/", StringComparison.Ordinal))
        {
            candidate = candidate[2..];
        }

        candidate = candidate.Trim('"');

        if (!int.TryParse(candidate, out var version) || version <= 0)
        {
            throw new BadRequestException("The If-Match header must contain a valid version.");
        }

        return version;
    }
}
