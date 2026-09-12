namespace AssistantCore.Repository.Queries;

public interface IBackofficeOrganizationQueries
{
    Task<BackofficeOrganizationListPageData> SearchOrganizationsAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);

    Task<BackofficeOrganizationDetailsData?> GetOrganizationDetailsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
