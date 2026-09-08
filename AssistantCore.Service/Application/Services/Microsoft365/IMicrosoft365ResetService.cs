using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ResetService
{
    /// <summary>
    /// Remet l'organisation courante devant une selection de sites vierge, sans
    /// redemander le consentement administrateur.
    /// </summary>
    Task<Microsoft365ResetResult> ResetSelectionAndIndexingAsync(
        CancellationToken cancellationToken = default);
}
