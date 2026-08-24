using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365UserGroupResolver
{
    Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
        Organization organization,
        string entraUserId,
        CancellationToken cancellationToken);
}
