namespace AssistantCore.Service.Application.Exceptions;

public sealed class AiProviderInvalidResponseException(string providerName)
    : AiProviderException(
        providerName,
        "AI_PROVIDER_INVALID_RESPONSE",
        "The AI provider returned an invalid response.");
