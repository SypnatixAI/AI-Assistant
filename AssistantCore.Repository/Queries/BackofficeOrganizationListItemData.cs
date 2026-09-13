using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Queries;

public sealed record BackofficeOrganizationListItemData(
    Guid Id,
    string Name,
    string? TenantId,
    RecordStatus Status,
    int UserCount,
    int ConnectorCount,
    int IndexedDocumentCount,
    DateTimeOffset? LastSyncAt);
