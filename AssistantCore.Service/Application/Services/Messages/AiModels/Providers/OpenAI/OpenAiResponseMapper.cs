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
    private const int MaximumProgressMessageLength = 240;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiModelResponse Map(OpenAiResponsesResult response)
    {
        var requestedToolCalls = MapToolCalls(response.ToolCalls);
        var decision = requestedToolCalls.Count > 0
            ? CreateUseToolsDecision(
                requestedToolCalls,
                TryReadProgressMessage(response.OutputText))
            : CreateTextDecision(response.OutputText);

        var usage = new AiModelUsage(
            response.InputTokens,
            response.OutputTokens,
            ModelCallCount: 1,
            ToolCallCount: requestedToolCalls.Count,
            EstimatedCost: null);

        return new AiModelResponse(
            decision,
            usage,
            new AiModelContinuationContext(
                OpenAiModelProvider.OpenAiProviderName,
                response.ResponseId));
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

    private static AiModelDecision CreateUseToolsDecision(
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls,
        string? progressMessage) =>
        new(
            AiModelDecisionType.UseTools,
            "The model requested one or more tools.",
            requestedToolCalls,
            Answer: null,
            CitedEvidenceIds: [],
            ProgressMessage: progressMessage);

    private static AiModelDecision CreateTextDecision(string outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw CreateInvalidResponseException();
        }

        var decision = JsonSerializer.Deserialize<OpenAiDecision>(
            outputText,
            SerializerOptions)
            ?? throw CreateInvalidResponseException();

        if (string.IsNullOrWhiteSpace(decision.Reason))
        {
            throw CreateInvalidResponseException();
        }

        var action = decision.Decision?.Trim().ToLowerInvariant() switch
        {
            "answer" => AiModelDecisionType.Answer,
            "askclarification" => AiModelDecisionType.AskClarification,
            "cannotanswer" => AiModelDecisionType.InsufficientInformation,
            _ => throw CreateInvalidResponseException()
        };

        if (string.IsNullOrWhiteSpace(decision.Answer))
        {
            throw CreateInvalidResponseException();
        }

        return new AiModelDecision(
            action,
            decision.Reason,
            ToolCalls: [],
            decision.Answer,
            decision.EvidenceIds ?? [],
            NormalizeProgressMessage(decision.ProgressMessage));
    }

    private static string? TryReadProgressMessage(string outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        try
        {
            var decision = JsonSerializer.Deserialize<OpenAiDecision>(
                outputText,
                SerializerOptions);
            return NormalizeProgressMessage(decision?.ProgressMessage);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeProgressMessage(string? progressMessage)
    {
        if (string.IsNullOrWhiteSpace(progressMessage))
        {
            return null;
        }

        var normalized = progressMessage.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= MaximumProgressMessageLength
            ? normalized
            : $"{normalized[..(MaximumProgressMessageLength - 1)].TrimEnd()}…";
    }

    private static AiProviderInvalidResponseException CreateInvalidResponseException() =>
        new(OpenAiModelProvider.OpenAiProviderName);

    private sealed record OpenAiDecision(
        [property: JsonPropertyName("decision")] string? Decision,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("answer")] string? Answer,
        [property: JsonPropertyName("progressMessage")] string? ProgressMessage,
        [property: JsonPropertyName("evidenceIds")] IReadOnlyCollection<string>? EvidenceIds);
}
