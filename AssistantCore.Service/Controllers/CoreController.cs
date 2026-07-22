using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.AuthenticateUser;
using AssistantCore.Service.Application.Commands.AuthenticateUser.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoreController(IDispatcher dispatcher) : ControllerBase
{
    [Authorize]
    [HttpGet("authenticateUser")]
    [SwaggerOperation(Summary = "Identifier l'utilisateur à partir du JWT")]
    [SwaggerResponse(StatusCodes.Status200OK, "Authentication request handled successfully.", typeof(AuthenticateUserResponse))]
    public async Task<ActionResult<AuthenticateUserResponse>> AuthenticateUser(CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new AuthenticateUserCommand(), cancellationToken);
        return Ok(result);
    }
}
