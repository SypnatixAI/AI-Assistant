using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Backoffice;

namespace AssistantCore.Service.Application.Services.Backoffice;

public sealed class BackofficeOrganizationService(
    IBackofficeOrganizationQueries organizationQueries)
    : IBackofficeOrganizationService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;

    public async Task<BackofficeOrganizationListResponse> SearchOrganizationsAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page <= 0 ? DefaultPage : page;
        var normalizedPageSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaximumPageSize);

        var result = await organizationQueries.SearchOrganizationsAsync(
            normalizedPage,
            normalizedPageSize,
            search,
            cancellationToken);

        return BackofficeOrganizationListResponse.FromData(result);
    }

    public async Task<BackofficeOrganizationDetailsDto> GetOrganizationDetailsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("Organization identifier is required.");
        }

        var data = await organizationQueries.GetOrganizationDetailsAsync(
            organizationId,
            cancellationToken);

        return data is null
            ? throw new NotFoundException("Organization not found.")
            : BackofficeOrganizationDetailsDto.FromData(data);
    }
}
