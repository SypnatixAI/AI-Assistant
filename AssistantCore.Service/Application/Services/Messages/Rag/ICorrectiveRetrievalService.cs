using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public interface ICorrectiveRetrievalService
{
    Task<IReadOnlyCollection<Microsoft365SearchRecord>> RetrieveAsync(
        Microsoft365SearchParameters parameters, ConnectorExecutionContext context,
        Func<Microsoft365SearchParameters, CancellationToken, Task<IReadOnlyCollection<Microsoft365SearchRecord>>> authorizedSearch,
        CancellationToken cancellationToken);
}
