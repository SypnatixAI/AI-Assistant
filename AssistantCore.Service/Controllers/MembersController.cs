using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMembers;
using AssistantCore.Service.Application.Commands.GetMembers.Models;
using AssistantCore.Service.Application.Commands.UpdateMemberRole;
using AssistantCore.Service.Application.Commands.UpdateMemberRole.Models;
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
}
