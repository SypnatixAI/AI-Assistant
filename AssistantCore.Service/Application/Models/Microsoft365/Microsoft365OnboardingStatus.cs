namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365OnboardingStatus(
    bool IsAdministrator,
    string ConnectionStatus,
    bool IsConsentComplete,
    bool HasSelectedSite,
    bool HasIndexedSource)
{
    public bool IsComplete =>
        IsConsentComplete && HasSelectedSite;
}
