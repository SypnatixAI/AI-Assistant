namespace AssistantCore.Service.Application.Exceptions;

public sealed class AiProviderTimeoutException(string providerName)
    : AiProviderException(
        providerName,
        "AI_PROVIDER_TIMEOUT",
        "The AI provider did not respond within the allowed time.");
