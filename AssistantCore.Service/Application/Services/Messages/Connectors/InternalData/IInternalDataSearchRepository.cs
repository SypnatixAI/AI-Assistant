using AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.InternalData;

public interface IInternalDataSearchRepository
{
    Task<IReadOnlyCollection<InternalDataSearchRecord>> SearchAsync(
        InternalDataSearchParameters parameters,
        CancellationToken cancellationToken);
}
