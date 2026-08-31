using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using ApplicationOpenAi = AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;
using ApplicationOpenAiResponsesResult = AssistantCore.Service.Application.Services.Messages.AiModels.Adapters.OpenAI.OpenAiResponsesResult;

namespace AssistantCore.Service.Infrastructure.AiModels.OpenAI;

public sealed class OpenAiResponsesClientAdapter(
    OpenAiResponsesClient externalClient,
    OpenAiResponsesRequestAdapter requestAdapter) : ApplicationOpenAi.IOpenAiResponsesClient
{
    public async Task<ApplicationOpenAiResponsesResult> CreateResponseAsync(
        AiModelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var externalRequest = requestAdapter.Map(request);
            var response = await externalClient.CreateResponseAsync(
                externalRequest,
                cancellationToken);

            return new ApplicationOpenAiResponsesResult(
                response.ResponseId,
                response.OutputText,
                response.ToolCalls.Select(toolCall => new ApplicationOpenAi.OpenAiToolCall(
                    toolCall.CallId,
                    toolCall.Name,
                    toolCall.ArgumentsJson)).ToArray(),
                response.InputTokens,
                response.OutputTokens);
        }
        catch (OpenAiExternalException exception)
        {
            throw new ApplicationOpenAi.OpenAiTransportException(exception.StatusCode);
        }
    }

    public async Task<ApplicationOpenAiResponsesResult> CreateResponseStreamingAsync(
        AiModelRequest request,
        Func<string, CancellationToken, ValueTask> onTextDelta,
        CancellationToken cancellationToken)
    {
        try
        {
            var externalRequest = requestAdapter.Map(request);
            var response = await externalClient.CreateResponseStreamingAsync(
                externalRequest,
                onTextDelta,
                cancellationToken);

            return new ApplicationOpenAiResponsesResult(
                response.ResponseId,
                response.OutputText,
                response.ToolCalls.Select(toolCall => new ApplicationOpenAi.OpenAiToolCall(
                    toolCall.CallId,
                    toolCall.Name,
                    toolCall.ArgumentsJson)).ToArray(),
                response.InputTokens,
                response.OutputTokens);
        }
        catch (OpenAiExternalException exception)
        {
            throw new ApplicationOpenAi.OpenAiTransportException(exception.StatusCode);
        }
    }

    public async Task<string> CreateConversationSummaryAsync(
        AiConversationSummaryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await externalClient.CreateConversationSummaryAsync(
                requestAdapter.Map(request),
                cancellationToken);
        }
        catch (OpenAiExternalException exception)
        {
            throw new ApplicationOpenAi.OpenAiTransportException(exception.StatusCode);
        }
    }

}
