using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent;
using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent.Models;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection.Models;
using AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists;
using AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists.Models;
using AssistantCore.Service.Application.Commands.EnableMicrosoft365List;
using AssistantCore.Service.Application.Commands.EnableMicrosoft365List.Models;
using AssistantCore.Service.Application.Models.Microsoft365;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

[ApiController]
[Route("api/microsoft365")]
[Authorize]
public sealed class Microsoft365Controller(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost("consent")]
    [SwaggerOperation(Summary = "Démarrer le consentement administrateur Microsoft 365")]
    [SwaggerResponse(StatusCodes.Status200OK, "Consent URL created.", typeof(StartMicrosoft365ConsentResponse))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Administrator access required.")]
    public async Task<ActionResult<StartMicrosoft365ConsentResponse>> StartConsent(
        CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(
            new StartMicrosoft365ConsentCommand(),
            cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("consent/callback")]
    [SwaggerOperation(Summary = "Traiter le retour du consentement Microsoft 365")]
    [SwaggerResponse(StatusCodes.Status200OK, "Microsoft 365 tenant connected.", typeof(CompleteMicrosoft365ConsentResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Consent callback is invalid.")]
    public async Task<ActionResult<CompleteMicrosoft365ConsentResponse>> CompleteConsent(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery(Name = "error")] string? microsoftError,
        CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(
            new CompleteMicrosoft365ConsentCommand(
                code ?? string.Empty,
                state ?? string.Empty,
                microsoftError),
            cancellationToken);
        return Ok(response);
    }

    [HttpDelete("connections/{connectionId:guid}")]
    [SwaggerOperation(Summary = "Révoquer une connexion Microsoft 365")]
    [SwaggerResponse(StatusCodes.Status200OK, "Microsoft 365 connection revoked.", typeof(RevokeMicrosoft365ConnectionResponse))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Administrator access required.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Microsoft 365 connection not found.")]
    public async Task<ActionResult<RevokeMicrosoft365ConnectionResponse>> RevokeConnection(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(
            new RevokeMicrosoft365ConnectionCommand(connectionId),
            cancellationToken);
        return Ok(response);
    }

    [HttpGet("sites/{siteId}/lists")]
    [SwaggerOperation(Summary = "Consulter les listes d'un site Microsoft 365")]
    [SwaggerResponse(StatusCodes.Status200OK, "Microsoft 365 site lists returned.", typeof(GetMicrosoft365SiteListsResponse))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Administrator access required.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Microsoft 365 site not found.")]
    public async Task<ActionResult<GetMicrosoft365SiteListsResponse>> GetSiteLists(
        string siteId,
        CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(
            new GetMicrosoft365SiteListsCommand(siteId),
            cancellationToken);
        return Ok(response);
    }

    [HttpPatch("sites/{siteId}/lists/{listId}")]
    [SwaggerOperation(Summary = "Modifier l'indexation d'une liste Microsoft 365")]
    [SwaggerResponse(StatusCodes.Status200OK, "Microsoft 365 list indexing updated.", typeof(Microsoft365ListResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Microsoft 365 list indexing cannot be updated.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Administrator access required.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Microsoft 365 list not found.")]
    public async Task<ActionResult<Microsoft365ListResponse>> EnableList(
        string siteId,
        string listId,
        [FromBody] EnableMicrosoft365ListRequest request,
        CancellationToken cancellationToken)
    {
        var response = await dispatcher.SendAsync(
            new EnableMicrosoft365ListCommand(
                siteId,
                listId,
                request.IsIndexed),
            cancellationToken);
        return Ok(response);
    }
}
