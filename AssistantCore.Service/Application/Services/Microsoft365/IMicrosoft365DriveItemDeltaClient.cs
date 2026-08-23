using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DriveItemDeltaClient
{
    IAsyncEnumerable<Microsoft365DriveItemDeltaPage> GetInitialPagesAsync(
        string tenantId,
        string driveId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<Microsoft365DriveItemDeltaPage> GetDeltaPagesAsync(
        string tenantId,
        string deltaLink,
        CancellationToken cancellationToken = default);
}
