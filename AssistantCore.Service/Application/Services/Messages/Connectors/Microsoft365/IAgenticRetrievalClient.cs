using AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IAgenticRetrievalClient
{
    Task<AgenticRetrievalResult> RetrieveAsync(
        AgenticRetrievalRequest request,
        CancellationToken cancellationToken);
}
