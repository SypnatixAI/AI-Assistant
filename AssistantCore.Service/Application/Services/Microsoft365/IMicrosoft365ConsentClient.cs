using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ConsentClient
{
    Uri CreateAdminConsentUri(string state);

    Task<Microsoft365ConsentExchange> CompleteAdminConsentAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
