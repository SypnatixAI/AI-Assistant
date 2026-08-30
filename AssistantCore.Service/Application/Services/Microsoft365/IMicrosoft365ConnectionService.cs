using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ConnectionService
{
    Task<Uri> StartConsentAsync(CancellationToken cancellationToken = default);

    Task<Microsoft365ConsentCompletionResult> CompleteConsentAsync(
        string tenantId,
        bool adminConsent,
        string state,
        string? microsoftError,
        CancellationToken cancellationToken = default);

    Task<Microsoft365ConnectionResult> RevokeAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);
}
