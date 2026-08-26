namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DriveContentClient
{
    Task<byte[]> DownloadAsync(
        string tenantId,
        string driveId,
        string driveItemId,
        CancellationToken cancellationToken = default);
}
