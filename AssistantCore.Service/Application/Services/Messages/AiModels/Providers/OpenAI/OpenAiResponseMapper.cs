using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AiModels.Adapters.OpenAI;

namespace AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

public sealed class OpenAiResponseMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiModelResponse Map(OpenAiResponsesResult response)
    {
        var requestedToolCalls = MapToolCalls(response.ToolCalls);
        var nextAction = requestedToolCalls.Count > 0
            ? CreateToolAction(requestedToolCalls)
            : CreateTextAction(response.OutputText);

        var usage = new AiModelUsage(
            response.InputTokens,
            response.OutputTokens,
            ModelCallCount: 1,
            ToolCallCount: requestedToolCalls.Count,
            EstimatedCost: null);

        return new AiModelResponse(nextAction, usage);
    }

    private static IReadOnlyCollection<AiRequestedToolCall> MapToolCalls(
        IReadOnlyCollection<OpenAiToolCall> toolCalls) =>
        toolCalls.Select(MapToolCall).ToArray();

    private static AiRequestedToolCall MapToolCall(OpenAiToolCall toolCall)
    {
        using var arguments = JsonDocument.Parse(toolCall.ArgumentsJson);

        return new AiRequestedToolCall(
            toolCall.CallId,
            toolCall.Name,
            arguments.RootElement.Clone());
    }

    private static AiModelNextAction CreateToolAction(
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls) =>
        new(
            AiModelNextActionType.ContinueWithTools,
            "The model requested one or more tools.",
            requestedToolCalls,
            ProposedAnswer: null,
            CitedEvidenceIds: []);

    private static AiModelNextAction CreateTextAction(string outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw CreateInvalidResponseException();
        }

        var decision = JsonSerializer.Deserialize<OpenAiDecision>(
            outputText,
            SerializerOptions)
            ?? throw CreateInvalidResponseException();

        if (string.IsNullOrWhiteSpace(decision.Reason)
            || string.IsNullOrWhiteSpace(decision.Answer))
        {
            throw CreateInvalidResponseException();
        }

        var action = decision.Decision?.Trim().ToLowerInvariant() switch
        {
            "answer" => AiModelNextActionType.ReturnAnswer,
            "cannotanswer" => AiModelNextActionType.CannotAnswer,
            _ => throw CreateInvalidResponseException()
        };

        return new AiModelNextAction(
            action,
            decision.Reason,
            RequestedToolCalls: [],
            decision.Answer,
            decision.EvidenceIds ?? []);
    }

    private static AiProviderInvalidResponseException CreateInvalidResponseException() =>
        new(OpenAiModelProvider.OpenAiProviderName);

    private sealed record OpenAiDecision(
        [property: JsonPropertyName("decision")] string? Decision,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("answer")] string? Answer,
        [property: JsonPropertyName("evidenceIds")] IReadOnlyCollection<string>? EvidenceIds);
}
