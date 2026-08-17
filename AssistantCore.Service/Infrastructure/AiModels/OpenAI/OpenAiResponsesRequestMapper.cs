using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.OpenAI.Models;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Tools;
using ApplicationOpenAi = AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

namespace AssistantCore.Service.Infrastructure.AiModels.OpenAI;

public sealed class OpenAiResponsesRequestMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public OpenAiResponsesRequest Map(AiModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var previousResponseId = MapPreviousResponseId(request.ContinuationContext);
        var isContinuation = previousResponseId is not null;

        return new OpenAiResponsesRequest(
            request.Model.ModelName,
            request.Instructions,
            isContinuation ? string.Empty : request.UserMessage,
            isContinuation
                ? []
                : request.ConversationHistory.Select(MapConversationMessage).ToArray(),
            request.AllowedTools.Select(MapToolDefinition).ToArray(),
            PreviousToolCalls: [],
            request.ToolResults.Select(MapToolResult).ToArray(),
            previousResponseId);
    }

    private static string? MapPreviousResponseId(
        AiModelContinuationContext? continuationContext)
    {
        if (continuationContext is null)
        {
            return null;
        }

        if (!string.Equals(
                continuationContext.Provider,
                ApplicationOpenAi.OpenAiModelProvider.OpenAiProviderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The continuation context does not belong to OpenAI.",
                nameof(continuationContext));
        }

        return continuationContext.Token;
    }

    private static OpenAiConversationMessage MapConversationMessage(
        AiConversationMessage message) =>
        new(
            message.Role switch
            {
                AiConversationRole.User => OpenAiConversationRole.User,
                AiConversationRole.Assistant => OpenAiConversationRole.Assistant,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(message),
                    message.Role,
                    "Unsupported conversation role.")
            },
            message.Content);

    private static OpenAiToolDefinition MapToolDefinition(AiToolDefinition tool) =>
        new(
            tool.Name,
            tool.Description,
            tool.InputSchema.GetRawText());

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
