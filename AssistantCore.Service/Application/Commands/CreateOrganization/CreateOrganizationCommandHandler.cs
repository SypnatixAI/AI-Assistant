using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Organizations;
using AssistantCore.Service.Application.Services.Organizations;

namespace AssistantCore.Service.Application.Commands.CreateOrganization;

public sealed class CreateOrganizationCommandHandler(
    IOrganizationManagementService organizationManagementService)
    : IRequestHandler<CreateOrganizationCommand, OrganizationResponse>
{
    public async Task<OrganizationResponse> HandleAsync(
        CreateOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var organization = await organizationManagementService.CreateOrganizationAsync(
            request.Name,
            request.IdentityProvider,
            request.ExternalTenantId,
            cancellationToken);

        return OrganizationResponse.FromOrganization(organization);
    }
}
