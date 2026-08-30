namespace AssistantCore.Service.Application.Commands.GetMicrosoft365OnboardingStatus.Models;

public sealed record GetMicrosoft365OnboardingStatusResponse(
    bool IsAdministrator,
    string ConnectionStatus,
    bool IsConsentComplete,
    bool HasSelectedSite,
    bool HasIndexedSource,
    bool IsComplete);
