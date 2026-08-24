using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365SearchRepository
{
    Task<IReadOnlyCollection<Microsoft365SearchRecord>> SearchAsync(
        Microsoft365SearchParameters parameters,
        CancellationToken cancellationToken);
}
