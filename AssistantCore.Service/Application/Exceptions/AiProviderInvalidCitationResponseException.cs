namespace AssistantCore.Service.Application.Exceptions;

public sealed class AiProviderInvalidCitationResponseException(string providerName)
    : AiProviderInvalidResponseException(providerName);
