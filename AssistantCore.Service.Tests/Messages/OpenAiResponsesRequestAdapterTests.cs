using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Infrastructure.AiModels.OpenAI;

namespace AssistantCore.Service.Tests.Messages;

public sealed class OpenAiResponsesRequestAdapterTests
{
    [Theory, AutoDomainData]
    public void Given_AnInitialRequest_When_Map_Then_IncludesQuestionAndConversationHistory(
        string question)
    {
        // Given
        var request = CreateRequest(question, continuationContext: null, toolResults: []);
        var adapter = new OpenAiResponsesRequestAdapter();

        // When
        var result = adapter.Map(request);

        // Then
        Assert.Null(result.PreviousResponseId);
        Assert.Equal(question, result.UserMessage);
        Assert.Equal(2, result.ConversationHistory.Count);
        Assert.Empty(result.PreviousToolCalls);
        Assert.Empty(result.ToolResults);
    }

    [Theory, AutoDomainData]
    public void Given_AContinuedRequest_When_Map_Then_UsesPreviousResponseWithoutPartialHistory(
        string question,
        string responseId,
        string callId)
    {
        // Given
        var continuation = new AiModelContinuationContext("OpenAI", responseId);
        var toolResult = ToolExecutionResult.Succeeded(callId, []);
        var request = CreateRequest(question, continuation, [toolResult]);
        var adapter = new OpenAiResponsesRequestAdapter();

        // When
        var result = adapter.Map(request);

        // Then
        Assert.Equal(responseId, result.PreviousResponseId);
        Assert.Empty(result.UserMessage);
        Assert.Empty(result.ConversationHistory);
        Assert.Empty(result.PreviousToolCalls);
        Assert.Equal(callId, Assert.Single(result.ToolResults).ToolCallId);
        Assert.Single(result.AvailableTools);
    }

    private static AiModelRequest CreateRequest(
        string question,
        AiModelContinuationContext? continuationContext,
        IReadOnlyCollection<ToolExecutionResult> toolResults)
    {
        var previousToolCall = new AiRequestedToolCall(
            "call-previous",
            AiToolNames.SearchInternalData,
            JsonSerializer.SerializeToElement(new { query = "previous" }));

        return new AiModelRequest(
            new SelectedAiModel("OpenAI", "gpt-5.6-luna"),
            "Orchestration instructions.",
            question,
            [
                new AiConversationMessage(AiConversationRole.User, "Previous question"),
                new AiConversationMessage(AiConversationRole.Assistant, "Previous answer")
            ],
            [
                new AiToolDefinition(
                    AiToolNames.SearchInternalData,
                    "Search internal data.",
                    JsonSerializer.SerializeToElement(new { type = "object" }))
            ],
            [previousToolCall],
            toolResults,
            continuationContext);
    }
}
