namespace AssistantCore.Service.Application.Exceptions;

public abstract class AiProviderException(
    string providerName,
    string technicalCode,
    string message) : Exception(message)
{
    public string ProviderName { get; } = providerName;

    public string TechnicalCode { get; } = technicalCode;
}
