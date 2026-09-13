using AssistantCore.Repository.Queries;

namespace AssistantCore.Service.Application.Models.Backoffice;

public sealed record BackofficeOrganizationListResponse(
    IReadOnlyCollection<BackofficeOrganizationListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public static BackofficeOrganizationListResponse FromData(
        BackofficeOrganizationListPageData data) => new(
            data.Items.Select(BackofficeOrganizationListItemDto.FromData).ToList(),
            data.Page,
            data.PageSize,
            data.TotalCount);
}
