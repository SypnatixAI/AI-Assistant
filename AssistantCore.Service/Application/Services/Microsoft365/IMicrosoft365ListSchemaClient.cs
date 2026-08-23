using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ListSchemaClient
{
    Task<IReadOnlyCollection<Microsoft365ListColumn>> GetColumnsAsync(
        string tenantId,
        string siteId,
        string listId,
        CancellationToken cancellationToken = default);
}
