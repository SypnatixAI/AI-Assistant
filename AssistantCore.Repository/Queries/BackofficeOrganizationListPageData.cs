namespace AssistantCore.Repository.Queries;

public sealed record BackofficeOrganizationListPageData(
    IReadOnlyCollection<BackofficeOrganizationListItemData> Items,
    int Page,
    int PageSize,
    int TotalCount);
