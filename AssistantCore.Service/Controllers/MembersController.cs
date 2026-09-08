using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMembers;
using AssistantCore.Service.Application.Commands.GetMembers.Models;
using AssistantCore.Service.Application.Commands.UpdateMemberRole;
using AssistantCore.Service.Application.Commands.UpdateMemberRole.Models;
using AssistantCore.Service.Application.Commands.UpdateMemberStatus;
using AssistantCore.Service.Application.Commands.UpdateMemberStatus.Models;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/members")]
public sealed class MembersController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Afficher les membres de l'organisation courante")]
    [SwaggerResponse(StatusCodes.Status200OK, "Members returned successfully.", typeof(GetMembersResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Administrator access required.")]
    public async Task<ActionResult<GetMembersResponse>> GetMembers(CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new GetMembersCommand(), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{memberId}/role")]
    [SwaggerOperation(Summary = "Modifier le role d'un membre de l'organisation courante")]
    [SwaggerResponse(StatusCodes.Status200OK, "Member role updated successfully.", typeof(MemberResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid member or role.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Administrator access required.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Member not found.")]
    public async Task<ActionResult<MemberResponse>> UpdateMemberRole(
        Guid memberId,
        [FromBody] UpdateMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new UpdateMemberRoleCommand(memberId, request.Role),
            cancellationToken);

        return Ok(result);
    }

    [HttpPatch("{memberId}/status")]
    [SwaggerOperation(Summary = "Activer ou desactiver un membre de l'organisation courante")]
    [SwaggerResponse(StatusCodes.Status200OK, "Member status updated successfully.", typeof(MemberResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid member id, status, or self-modification.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Tenant administrator access required.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Member not found.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The member was modified in another session.")]
    public async Task<ActionResult<MemberResponse>> UpdateMemberStatus(
        Guid memberId,
        [FromBody] UpdateMemberStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new UpdateMemberStatusCommand(memberId, request.Status, ReadExpectedVersion()),
            cancellationToken);

        return Ok(result);
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
