using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Backoffice;
using AssistantCore.Service.Application.Services.Backoffice;

namespace AssistantCore.Service.Application.Commands.GetBackofficeOrganizations;

public sealed class GetBackofficeOrganizationsQueryHandler(
    IBackofficeOrganizationService organizationService)
    : IRequestHandler<GetBackofficeOrganizationsQuery, BackofficeOrganizationListResponse>
{
    public async Task<BackofficeOrganizationListResponse> HandleAsync(
        GetBackofficeOrganizationsQuery request,
        CancellationToken cancellationToken)
    {
        return await organizationService.SearchOrganizationsAsync(
            request.Page,
            request.PageSize,
            request.Search,
            cancellationToken);
    }
}
