using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Orchestration;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiModelTurnServiceTests
{
    [Theory]
    [InlineAutoDomainData(AiModelDecisionType.Answer)]
    [InlineAutoDomainData(AiModelDecisionType.UseTools)]
    [InlineAutoDomainData(AiModelDecisionType.InsufficientInformation)]
    public async Task Given_AProviderDecision_When_RequestNextActionAsync_Then_ReturnsTheDecision(
        AiModelDecisionType decisionType,
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var expectedResponse = CreateResponse(decisionType);
        var provider = new RecordingAiModelProvider("OpenAI", expectedResponse);
        var state = CreateState(processing, startedAtUtc);
        var service = new AiModelTurnService(
            [provider],
            new StubTimeProvider(startedAtUtc.AddSeconds(1)));

        // When
        var result = await service.RequestNextActionAsync(state, CancellationToken.None);

        // Then
        Assert.Same(expectedResponse, result);
        Assert.Same(expectedResponse.ContinuationContext, state.ContinuationContext);
        Assert.Equal(15, state.Budget.Usage.ModelTokenCount);
        Assert.Equal(10, state.Budget.Usage.ContextSize);
        Assert.Equal(0.01m, state.Budget.Usage.EstimatedCost);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInitializedExecution_When_RequestNextActionAsync_Then_SendsQuestionHistoryAndAllowedTools(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var provider = new RecordingAiModelProvider(
            "OpenAI",
            CreateResponse(AiModelDecisionType.UseTools));
        var state = CreateState(processing, startedAtUtc);
        var service = new AiModelTurnService(
            [provider],
            new StubTimeProvider(startedAtUtc.AddSeconds(1)));

        // When
        await service.RequestNextActionAsync(state, CancellationToken.None);

        // Then
        var request = Assert.IsType<AiModelRequest>(provider.ReceivedRequest);
        Assert.Same(state.SelectedModel, request.Model);
        Assert.Equal(state.Question, request.UserMessage);
        Assert.Equal(state.ConversationHistory, request.ConversationHistory);
        Assert.Equal(state.AllowedTools, request.AllowedTools);
        Assert.Empty(request.RequestedToolCalls);
        Assert.Empty(request.ToolResults);
        Assert.Contains("answer or cannotAnswer", request.Instructions, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnregisteredProvider_When_RequestNextActionAsync_Then_RejectsTheCall(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        var service = new AiModelTurnService(
            [],
            new StubTimeProvider(startedAtUtc.AddSeconds(1)));

        // When
        var exception = await Record.ExceptionAsync(() =>
            service.RequestNextActionAsync(state, CancellationToken.None));

        // Then
        var invalidOperationException = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(
            "The selected AI model provider is not uniquely registered.",
            invalidOperationException.Message);
    }

    private static MessageOrchestrationState CreateState(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        var history = new[]
        {
            new AiConversationMessage(AiConversationRole.User, "Previous question"),
            new AiConversationMessage(AiConversationRole.Assistant, "Previous answer")
        };
        var tools = new[]
        {
            new AiToolDefinition(
                AiToolNames.SearchInternalData,
                "Search internal data.",
                JsonSerializer.SerializeToElement(new { type = "object" }))
        };
        var limits = new OrchestrationExecutionLimits(
            MaximumExecutionTime: TimeSpan.FromMinutes(2),
            MaximumToolCalls: 8,
            MaximumModelTokens: 12_000,
            MaximumEstimatedCost: 1.25m,
            MaximumResultsPerTool: 20,
            MaximumContextSize: 30_000,
            MaximumRepeatedToolCalls: 2);

        return MessageOrchestrationState.Start(
            processing,
            new SelectedAiModel("OpenAI", "gpt-5.6-luna"),
            history,
            tools,
            limits,
            startedAtUtc);
    }

    private static AiModelResponse CreateResponse(AiModelDecisionType decisionType) => new(
        new AiModelDecision(
            decisionType,
            "Reason",
            [],
            decisionType == AiModelDecisionType.UseTools ? null : "Answer",
            []),
        new AiModelUsage(
            InputTokens: 10,
            OutputTokens: 5,
            ModelCallCount: 1,
            ToolCallCount: 0,
            EstimatedCost: 0.01m),
        new AiModelContinuationContext("OpenAI", "response-001"));

    private sealed class RecordingAiModelProvider(
        string providerName,
        AiModelResponse response) : IAiModelProvider
    {
        public string ProviderName => providerName;

        public AiModelRequest? ReceivedRequest { get; private set; }

        public Task<AiModelResponse> GetNextActionAsync(
            AiModelRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedRequest = request;
            return Task.FromResult(response);
        }
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
