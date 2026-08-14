namespace AssistantCore.Service.Application.Exceptions;

public sealed class AiProviderLimitException(string providerName)
    : AiProviderException(
        providerName,
        "AI_PROVIDER_LIMIT",
        "The AI provider limit or quota was reached.");
