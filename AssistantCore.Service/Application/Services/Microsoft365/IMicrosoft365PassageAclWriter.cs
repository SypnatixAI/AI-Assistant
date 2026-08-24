using AssistantCore.Service.Application.Models.Microsoft365.Permissions;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365PassageAclWriter
{
    Task SetAvailabilityAsync(
        IReadOnlyCollection<string> chunkIds,
        bool isAvailable,
        CancellationToken cancellationToken = default);

    Task UpdateAclAsync(
        IReadOnlyCollection<string> chunkIds,
        Microsoft365Acl acl,
        CancellationToken cancellationToken = default);
}
