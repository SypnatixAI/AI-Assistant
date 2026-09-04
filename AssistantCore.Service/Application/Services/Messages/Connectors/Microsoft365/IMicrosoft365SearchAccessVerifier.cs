using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365SearchAccessVerifier
{
    Task<IReadOnlyCollection<Microsoft365SearchRecord>> KeepAuthorizedAsync(
        Guid organizationId,
        string externalTenantId,
        string entraUserId,
        IReadOnlyCollection<string> entraGroupIds,
        IReadOnlyCollection<Microsoft365SearchRecord> records,
        CancellationToken cancellationToken);
}
