using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365CurrentUserOneDriveClientAdapter(
    MicrosoftGraphClient graphClient,
    IMicrosoft365ApplicationTokenClient applicationTokenClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365CurrentUserOneDriveClient
{
    public async Task<Microsoft365CurrentUserDrive?> GetAsync(
        string tenantId,
        string entraUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entraUserId);

        var accessToken = await applicationTokenClient.AcquireGraphTokenAsync(
            tenantId,
            cancellationToken);
        var drive = await graphClient.GetUserDriveAsync(
            options.Value.GraphBaseUrl,
            accessToken,
            entraUserId,
            cancellationToken);

        return drive is null
            ? null
            : new Microsoft365CurrentUserDrive(
                drive.DriveId,
                drive.DisplayName,
                drive.WebUrl);
    }
}
