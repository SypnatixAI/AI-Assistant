namespace AssistantCore.ExternalServices.Services.OpenAI;

public sealed class OpenAiExternalException(
    int statusCode,
    string? providerErrorMessage = null)
    : Exception("The OpenAI request failed.")
{
    public int StatusCode { get; } = statusCode;

    public string? ProviderErrorMessage { get; } = providerErrorMessage;
}
