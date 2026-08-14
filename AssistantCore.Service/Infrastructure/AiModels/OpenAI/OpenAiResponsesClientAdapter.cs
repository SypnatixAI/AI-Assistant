using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.OpenAI.Models;
using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Tools;
using ApplicationOpenAi = AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;
using ApplicationOpenAiResponsesResult = AssistantCore.Service.Application.Services.Messages.AiModels.Adapters.OpenAI.OpenAiResponsesResult;

namespace AssistantCore.Service.Infrastructure.AiModels.OpenAI;

public sealed class OpenAiResponsesClientAdapter(
    OpenAiResponsesClient externalClient) : ApplicationOpenAi.IOpenAiResponsesClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ApplicationOpenAiResponsesResult> CreateResponseAsync(
        AiModelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var externalRequest = MapRequest(request);
            var response = await externalClient.CreateResponseAsync(
                externalRequest,
                cancellationToken);

            return new ApplicationOpenAiResponsesResult(
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

    private static OpenAiResponsesRequest MapRequest(AiModelRequest request) =>
        new(
            request.Model.ModelName,
            request.Instructions,
            request.UserMessage,
            request.AvailableTools.Select(MapToolDefinition).ToArray(),
            request.PreviousToolCalls.Select(MapPreviousToolCall).ToArray(),
            request.ToolResults.Select(MapToolResult).ToArray());

    private static OpenAiToolDefinition MapToolDefinition(AiToolDefinition tool) =>
        new(
            tool.Name,
            tool.Description,
            tool.InputSchema.GetRawText());

    private static OpenAiPreviousToolCall MapPreviousToolCall(AiRequestedToolCall toolCall) =>
        new(
            toolCall.CallId,
            toolCall.ToolName,
            toolCall.Arguments.GetRawText());

    private static OpenAiToolResult MapToolResult(ToolExecutionResult result) =>
        new(
            result.ToolCallId,
            JsonSerializer.Serialize(
                new
                {
                    status = result.Status,
                    evidence = result.Evidence,
                    warnings = result.Warnings,
                    errorCode = result.ErrorCode
                },
                SerializerOptions));
}
