using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ConsentClient
{
    Uri CreateAdminConsentUri(string state);

    Task<Microsoft365ConsentExchange> CompleteAdminConsentAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifie que le token applicatif peut reellement utiliser les
    /// permissions requises par le connecteur. Retourne false lorsque Microsoft
    /// Graph refuse l'appel representatif avec 403 (permissions absentes ou
    /// retirees) ; leve <see cref="AssistantCore.Service.Application.Exceptions.Microsoft365ExternalException"/>
    /// pour tout autre echec technique.
    /// </summary>
    Task<bool> VerifyRequiredPermissionsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}
