namespace AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

public sealed class OpenAiTransportException(
    int statusCode,
    string? providerErrorMessage = null)
    : Exception("The OpenAI request failed.")
{
    public int StatusCode { get; } = statusCode;

    public string? ProviderErrorMessage { get; } = providerErrorMessage;
}
