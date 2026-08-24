using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365AclResolver
{
    Task<Microsoft365AclResolution> ResolveAsync(
        Organization organization,
        Microsoft365ContentReference contentReference,
        CancellationToken cancellationToken = default);
}
