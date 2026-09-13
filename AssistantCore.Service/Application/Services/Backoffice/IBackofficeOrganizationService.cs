using AssistantCore.Service.Application.Models.Backoffice;

namespace AssistantCore.Service.Application.Services.Backoffice;

public interface IBackofficeOrganizationService
{
    Task<BackofficeOrganizationListResponse> SearchOrganizationsAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<BackofficeOrganizationDetailsDto> GetOrganizationDetailsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
