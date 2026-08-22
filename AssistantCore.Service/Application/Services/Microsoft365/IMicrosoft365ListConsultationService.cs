using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ListConsultationService
{
    Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(
        string siteId,
        CancellationToken cancellationToken = default);
}
