using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Backoffice;
using AssistantCore.Service.Application.Services.Backoffice;

namespace AssistantCore.Service.Application.Commands.GetBackofficeOrganizationDetails;

public sealed class GetBackofficeOrganizationDetailsQueryHandler(
    IBackofficeOrganizationService organizationService)
    : IRequestHandler<GetBackofficeOrganizationDetailsQuery, BackofficeOrganizationDetailsDto>
{
    public async Task<BackofficeOrganizationDetailsDto> HandleAsync(
        GetBackofficeOrganizationDetailsQuery request,
        CancellationToken cancellationToken)
    {
        return await organizationService.GetOrganizationDetailsAsync(
            request.OrganizationId,
            cancellationToken);
    }
}
