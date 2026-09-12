using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Queries;

public sealed record BackofficeOrganizationDetailsData(
    BackofficeOrganizationData Organization,
    BackofficeOrganizationUsersData Users,
    BackofficeOrganizationMicrosoftData Microsoft,
    BackofficeOrganizationSourcesData Sources,
    BackofficeOrganizationIndexingData Indexing);

public sealed record BackofficeOrganizationData(
    Guid Id,
    string Name,
    string? TenantId,
    RecordStatus Status,
    DateTimeOffset CreatedAt);

public sealed record BackofficeOrganizationUsersData(
    int Total,
    int Active);

public sealed record BackofficeOrganizationMicrosoftData(
    bool Connected,
    bool AdminConsentGranted);

public sealed record BackofficeOrganizationSourcesData(
    int SharePointSiteCount,
    int OneDriveCount);

public sealed record BackofficeOrganizationIndexingData(
    int DocumentCount,
    DateTimeOffset? LastSyncAt,
    string Status);
