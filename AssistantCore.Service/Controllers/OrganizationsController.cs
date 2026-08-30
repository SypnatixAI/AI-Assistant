using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.CreateOrganization;
using AssistantCore.Service.Application.Commands.CreateOrganization.Models;
using AssistantCore.Service.Application.Models.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(Summary = "Créer une organisation")]
    [SwaggerResponse(StatusCodes.Status201Created, "Organization created successfully.", typeof(OrganizationResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid organization data.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "The required API scope is missing.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "An organization already exists for this tenant.")]
    public async Task<ActionResult<OrganizationResponse>> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new CreateOrganizationCommand(request.Domain),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
