using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetTokenUsage;
using AssistantCore.Service.Application.Models.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/usage")]
public sealed class UsageController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Consulter le quota de jetons de l'organisation courante")]
    [SwaggerResponse(StatusCodes.Status200OK, "Usage summary returned successfully.", typeof(TokenUsageResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Organization or member access denied.")]
    public async Task<ActionResult<TokenUsageResponse>> GetTokenUsage(CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new GetTokenUsageCommand(), cancellationToken);

        return Ok(result);
    }
}
