using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365DriveContentClientAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphDriveContentClient graphClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365DriveContentClient
{
    public async Task<byte[]> DownloadAsync(
        string tenantId,
        string driveId,
        string driveItemId,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var token = await identityClient.AcquireApplicationTokenAsync(
            configuration.AuthorityBaseUrl,
            tenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            cancellationToken);
        return await graphClient.DownloadAsync(
            configuration.GraphBaseUrl,
            token.AccessToken,
            driveId,
            driveItemId,
            configuration.MaximumExtractionFileSizeBytes,
            cancellationToken);
    }
}
