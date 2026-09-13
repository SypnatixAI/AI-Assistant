using AssistantCore.Repository.Queries;

namespace AssistantCore.Service.Application.Models.Backoffice;

public sealed record BackofficeOrganizationDetailsDto(
    BackofficeOrganizationDto Organization,
    BackofficeOrganizationUsersDto Users,
    BackofficeOrganizationMicrosoftDto Microsoft,
    BackofficeOrganizationSourcesDto Sources,
    BackofficeOrganizationIndexingDto Indexing)
{
    public static BackofficeOrganizationDetailsDto FromData(
        BackofficeOrganizationDetailsData data) => new(
            new BackofficeOrganizationDto(
                data.Organization.Id,
                data.Organization.Name,
                data.Organization.TenantId,
                BackofficeOrganizationStatusFormatter.Format(data.Organization.Status),
                data.Organization.CreatedAt),
            new BackofficeOrganizationUsersDto(
                data.Users.Total,
                data.Users.Active),
            new BackofficeOrganizationMicrosoftDto(
                data.Microsoft.Connected,
                data.Microsoft.AdminConsentGranted),
            new BackofficeOrganizationSourcesDto(
                data.Sources.SharePointSiteCount,
                data.Sources.OneDriveCount),
            new BackofficeOrganizationIndexingDto(
                data.Indexing.DocumentCount,
                data.Indexing.LastSyncAt,
                data.Indexing.Status));
}

public sealed record BackofficeOrganizationDto(
    Guid Id,
    string Name,
    string? TenantId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record BackofficeOrganizationUsersDto(
    int Total,
    int Active);

public sealed record BackofficeOrganizationMicrosoftDto(
    bool Connected,
    bool AdminConsentGranted);

public sealed record BackofficeOrganizationSourcesDto(
    int SharePointSiteCount,
    int OneDriveCount);

public sealed record BackofficeOrganizationIndexingDto(
    int DocumentCount,
    DateTimeOffset? LastSyncAt,
    string Status);
