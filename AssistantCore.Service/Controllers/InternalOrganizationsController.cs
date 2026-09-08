using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.SetUsagePolicy;
using AssistantCore.Service.Application.Commands.SetUsagePolicy.Models;
using AssistantCore.Service.Application.Models.Usage;
using AssistantCore.Service.Infrastructure.Authentication.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AssistantCore.Service.Controllers;

/// <summary>
/// Endpoints internes reserves aux operations Sypnatix : jamais appeles par Angular,
/// toujours proteges par un role applicatif dedie plutot que par le scope utilisateur.
/// </summary>
[ApiController]
[Authorize(Policy = UsagePolicyAuthorizationPolicies.Management)]
[Route("api/internal/organizations")]
public sealed class InternalOrganizationsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPut("{organizationId}/usage-policy")]
    [SwaggerOperation(Summary = "Planifier la politique de quota d'une organisation")]
    [SwaggerResponse(StatusCodes.Status200OK, "Usage policy recorded successfully.", typeof(UsagePolicyResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid limit, status, or effective date.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Authentication required.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Application usage-policy permission required.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Organization not found.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Concurrent or incompatible usage policy.")]
    public async Task<ActionResult<UsagePolicyResponse>> SetUsagePolicy(
        Guid organizationId,
        [FromBody] SetUsagePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new SetUsagePolicyCommand(
                organizationId,
                request.MonthlyTokenLimit,
                request.Status,
                request.EffectiveAt),
            cancellationToken);

        return Ok(result);
    }
}
