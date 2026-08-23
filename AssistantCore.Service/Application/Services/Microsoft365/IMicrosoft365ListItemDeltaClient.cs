using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ListItemDeltaClient
{
    IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetInitialPagesAsync(
        string tenantId,
        string siteId,
        string listId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<Microsoft365ListItemDeltaPage> GetDeltaPagesAsync(
        string tenantId,
        string deltaLink,
        CancellationToken cancellationToken = default);
}
