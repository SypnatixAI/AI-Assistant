namespace AssistantCore.Service.Application.Services.Microsoft365;

/// <summary>
/// Repond uniquement a "le setup Microsoft 365 de cette organisation est-il termine ?",
/// avec la meme definition que Microsoft365OnboardingStatus.IsComplete (connexion active
/// et au moins un site selectionne). Ne depend d'aucun service qui resout l'identite
/// courante (IAuthenticateUserService, IMessageUserContextService) : ces services
/// l'utilisent pour appliquer la politique d'admission, et une dependance inverse
/// creerait un cycle a l'injection de dependances.
/// </summary>
public interface IMicrosoft365OnboardingCompletionChecker
{
    Task<bool> IsCompleteAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
