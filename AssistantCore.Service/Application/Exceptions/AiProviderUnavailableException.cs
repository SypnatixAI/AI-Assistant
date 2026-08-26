namespace AssistantCore.Service.Application.Exceptions;

public sealed class AiProviderUnavailableException(
    string providerName,
    int? providerStatusCode = null)
    : AiProviderException(
        providerName,
        "AI_PROVIDER_UNAVAILABLE",
        "The AI provider is currently unavailable.")
{
    public int? ProviderStatusCode { get; } = providerStatusCode;
}
