using AssistantCore.Repository.Queries;

namespace AssistantCore.Service.Application.Models.Backoffice;

public sealed record BackofficeOrganizationListItemDto(
    Guid Id,
    string Name,
    string? TenantId,
    string Status,
    int UserCount,
    int ConnectorCount,
    int IndexedDocumentCount,
    DateTimeOffset? LastSyncAt)
{
    public static BackofficeOrganizationListItemDto FromData(
        BackofficeOrganizationListItemData data) => new(
            data.Id,
            data.Name,
            data.TenantId,
            BackofficeOrganizationStatusFormatter.Format(data.Status),
            data.UserCount,
            data.ConnectorCount,
            data.IndexedDocumentCount,
            data.LastSyncAt);
}
