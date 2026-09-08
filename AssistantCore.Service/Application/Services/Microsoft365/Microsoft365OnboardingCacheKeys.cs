namespace AssistantCore.Service.Application.Services.Microsoft365;

/// <summary>
/// Clefs du cache d'onboarding, partagees entre le service qui les ecrit et
/// celui qui les invalide. Deux chaines construites separement finiraient par
/// diverger, et le reset cesserait silencieusement d'invalider quoi que ce soit.
/// </summary>
public static class Microsoft365OnboardingCacheKeys
{
    public static string CompletionKey(Guid organizationId) =>
        $"m365-onboarding-complete:{organizationId:D}";
}
