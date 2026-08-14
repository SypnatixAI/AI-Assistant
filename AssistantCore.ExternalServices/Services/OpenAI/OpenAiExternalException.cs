namespace AssistantCore.ExternalServices.Services.OpenAI;

public sealed class OpenAiExternalException(int statusCode)
    : Exception("The OpenAI request failed.")
{
    public int StatusCode { get; } = statusCode;
}
