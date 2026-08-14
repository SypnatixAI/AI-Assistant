namespace AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

public sealed class OpenAiTransportException(int statusCode)
    : Exception("The OpenAI request failed.")
{
    public int StatusCode { get; } = statusCode;
}
