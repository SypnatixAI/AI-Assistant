using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ListActivationService
{
    Task<Microsoft365List> SetIndexingAsync(
        string siteId,
        string listId,
        bool isIndexed,
        CancellationToken cancellationToken = default);
}
