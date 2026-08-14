namespace AssistantCore.Service.Application.Exceptions;

public sealed class AiProviderUnavailableException(string providerName)
    : AiProviderException(
        providerName,
        "AI_PROVIDER_UNAVAILABLE",
        "The AI provider is currently unavailable.");
