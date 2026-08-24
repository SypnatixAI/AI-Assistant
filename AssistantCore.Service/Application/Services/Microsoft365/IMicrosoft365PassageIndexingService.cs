using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365PassageIndexingService
{
    Task<Microsoft365PassageIndexingResult> IndexAsync(
        Organization organization,
        Guid sourceId,
        Microsoft365ContentReference contentReference,
        IReadOnlyCollection<Microsoft365SearchPassage> passages,
        CancellationToken cancellationToken = default);
}
