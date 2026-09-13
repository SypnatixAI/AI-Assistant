using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetBackofficeOrganizationDetails;
using AssistantCore.Service.Application.Commands.GetBackofficeOrganizations;
using AssistantCore.Service.Application.Models.Backoffice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/backoffice/organizations")]
public sealed class BackofficeOrganizationsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Lister les organisations clientes pour le backoffice")]
    [SwaggerResponse(StatusCodes.Status200OK, "Organizations returned.", typeof(BackofficeOrganizationListResponse))]
    public async Task<ActionResult<BackofficeOrganizationListResponse>> GetOrganizations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var response = await dispatcher.SendAsync(
            new GetBackofficeOrganizationsQuery(page, pageSize, search),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{organizationId:guid}")]
    [SwaggerOperation(Summary = "Lire la fiche générale d'une organisation cliente")]
    [SwaggerResponse(StatusCodes.Status200OK, "Organization details returned.", typeof(BackofficeOrganizationDetailsDto))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Organization not found.")]
    public async Task<ActionResult<BackofficeOrganizationDetailsDto>> GetOrganizationDetails(
        [FromRoute] Guid organizationId,
        CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(
            new GetBackofficeOrganizationDetailsQuery(organizationId),
            cancellationToken);

        return Ok(response);
    }
}
