using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DriveAdministrationService
{
    Task<Microsoft365SiteResponse> RegisterSiteAsync(
        string siteId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Microsoft365DriveResponse>> GetDrivesAsync(
        string siteId,
        CancellationToken cancellationToken = default);

    Task<Microsoft365DriveResponse> EnableDriveAsync(
        string siteId,
        string driveId,
        bool isIndexed,
        CancellationToken cancellationToken = default);
}
