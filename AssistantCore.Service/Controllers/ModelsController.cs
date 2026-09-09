using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetAvailableModels;
using AssistantCore.Service.Application.Models.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/models")]
public sealed class ModelsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Lister les modeles disponibles pour l'organisation courante")]
    [SwaggerResponse(StatusCodes.Status200OK, "Available models returned successfully.", typeof(GetAvailableModelsResponse))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Organization or member access denied.")]
    public async Task<ActionResult<GetAvailableModelsResponse>> GetAvailableModels(
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new GetAvailableModelsCommand(), cancellationToken);

        return Ok(result);
    }
}
