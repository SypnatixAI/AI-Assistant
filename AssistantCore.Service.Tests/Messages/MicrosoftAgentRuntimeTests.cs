using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Messages;

public sealed class MicrosoftAgentRuntimeTests
{
    [Theory, AutoDomainData]
    public async Task Given_HistoryAndMessage_When_RunAsync_Then_SendsThemToTheSelectedProvider(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var history = new[]
        {
            new AiConversationMessage(AiConversationRole.User, "Quel est le statut?"),
            new AiConversationMessage(AiConversationRole.Assistant, "Je vais verifier.")
        };
        processing = processing with
        {
            UserMessage = "Et pour Atlas?",
            ConversationHistory = history
        };
        var provider = new RecordingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Atlas est en attente."));
        var runtime = CreateRuntime(provider);

        // When
        await runtime.RunAsync(
            new AgentTurnRequest(processing, executionContext, selectedModel),
            CancellationToken.None);

        // Then
        var request = Assert.Single(provider.ReceivedRequests);
        Assert.Same(selectedModel, request.Model);
        Assert.Equal("Et pour Atlas?", request.UserMessage);
        Assert.Equal(history, request.ConversationHistory);
        Assert.Empty(request.AllowedTools);
        Assert.Empty(request.RequestedToolCalls);
        Assert.Empty(request.ToolResults);
    }

    [Theory, AutoDomainData]
    public async Task Given_MultipleProviders_When_RunAsync_Then_UsesTheSelectedModelProvider(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var selectedProvider = new RecordingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Reponse OpenAI."));
        var otherProvider = new RecordingAiModelProvider(
            "Other",
            CreateResponse("Reponse autre."));
        var runtime = CreateRuntime(otherProvider, selectedProvider);

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, executionContext, selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal("Reponse OpenAI.", result.Content);
        Assert.Single(selectedProvider.ReceivedRequests);
        Assert.Empty(otherProvider.ReceivedRequests);
    }

    [Theory, AutoDomainData]
    public async Task Given_AProviderResponse_When_RunAsync_Then_ReturnsTheAgentTurnResult(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new RecordingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Voici la reponse.", inputTokens: 17, outputTokens: 9));
        var runtime = CreateRuntime(provider);

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, executionContext, selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal("Voici la reponse.", result.Content);
        Assert.Equal(selectedModel.ModelName, result.ModelName);
        Assert.Empty(result.Citations);
        Assert.Empty(result.Warnings);
        Assert.Equal(17, result.Usage.InputTokens);
        Assert.Equal(9, result.Usage.OutputTokens);
        Assert.Equal(1, result.Usage.ModelCallCount);
        Assert.Equal(0, result.Usage.ToolCallCount);
        Assert.Equal(17, result.Usage.ContextSize);
    }

    [Theory, AutoDomainData]
    public async Task Given_AStreamingProvider_When_RunStreamingAsync_Then_ForwardsDeltaBeforeFinalResponse(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new ControlledStreamingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Le projet", inputTokens: 21, outputTokens: 6),
            ["Le", " projet"]);
        var runtime = CreateRuntime(provider);
        var receivedDeltas = new List<string>();
        var callbacks = new AgentTurnStreamingCallbacks(
            (_, _) => ValueTask.CompletedTask,
            (delta, _) =>
            {
                receivedDeltas.Add(delta);
                provider.MarkDeltaObservedByRuntime();
                return ValueTask.CompletedTask;
            });

        // When
        var runtimeTask = runtime.RunStreamingAsync(
            new AgentTurnRequest(processing, executionContext, selectedModel),
            callbacks,
            CancellationToken.None);

        await provider.WaitUntilDeltaObservedByRuntimeAsync();

        // Then
        Assert.Equal(new[] { "Le" }, receivedDeltas);
        Assert.False(runtimeTask.IsCompleted);

        provider.CompleteFinalResponse();
        var result = await runtimeTask;
        Assert.Equal(new[] { "Le", " projet" }, receivedDeltas);
        Assert.Equal("Le projet", result.Content);
        Assert.Equal(21, result.Usage.InputTokens);
        Assert.Equal(6, result.Usage.OutputTokens);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACancelledToken_When_RunAsync_Then_PropagatesCancellationToTheProvider(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new RecordingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Annule."),
            throwOnRequest: true);
        var runtime = CreateRuntime(provider);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        // When
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runtime.RunAsync(
                new AgentTurnRequest(processing, executionContext, selectedModel),
                cancellationSource.Token));

        // Then
        Assert.True(provider.ReceivedCancellationToken.IsCancellationRequested);
    }

    private static MicrosoftAgentRuntime CreateRuntime(
        params IAiModelProvider[] providers) =>
        new(
            providers,
            new EmptyToolRegistry(),
            new ThrowingToolCallValidator(),
            new ThrowingToolExecutionRouter(),
            Options.Create(CreateOrchestrationOptions()),
            TimeProvider.System,
            NullLoggerFactory.Instance);

    private static MessageOrchestrationOptions CreateOrchestrationOptions() =>
        new()
        {
            MaximumExecutionTimeSeconds = 30,
            MaximumToolCalls = 4,
            MaximumModelTokens = 12_000,
            MaximumEstimatedCost = 1,
            RetrievalCandidateLimit = 10,
            FinalEvidenceLimit = 5,
            MaximumContextSize = 30_000,
            MaximumRepeatedToolCalls = 2,
            MaximumParallelToolCalls = 2
        };

    private static SelectedAiModel CreateSelectedModel() =>
        new("OpenAI", "gpt-test");

    private static AiModelResponse CreateResponse(
        string answer,
        int inputTokens = 10,
        int outputTokens = 5) =>
        new(
            new AiModelDecision(
                AiModelDecisionType.Answer,
                "The model answered.",
                ToolCalls: [],
                answer,
                CitedEvidenceIds: []),
            new AiModelUsage(
                inputTokens,
                outputTokens,
                ModelCallCount: 1,
                ToolCallCount: 0,
                EstimatedCost: null));

    private sealed class RecordingAiModelProvider(
        string providerName,
        AiModelResponse response,
        bool throwOnRequest = false) : IAiModelProvider
    {
        public string ProviderName => providerName;

        public List<AiModelRequest> ReceivedRequests { get; } = [];

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<AiModelResponse> GetNextActionAsync(
            AiModelRequest request,
            CancellationToken cancellationToken)
        {
            ReceivedRequests.Add(request);
            ReceivedCancellationToken = cancellationToken;

            return throwOnRequest
                ? Task.FromCanceled<AiModelResponse>(cancellationToken)
                : Task.FromResult(response);
        }

        public Task<AiModelResponse> GetNextActionStreamingAsync(
            AiModelRequest request,
            Func<string, CancellationToken, ValueTask> onTextDelta,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ControlledStreamingAiModelProvider(
        string providerName,
        AiModelResponse response,
        IReadOnlyCollection<string> deltas) : IAiModelProvider
    {
        private readonly TaskCompletionSource _deltaObservedByRuntime =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _completeFinalResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ProviderName => providerName;

        public Task<AiModelResponse> GetNextActionAsync(
            AiModelRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<AiModelResponse> GetNextActionStreamingAsync(
            AiModelRequest request,
            Func<string, CancellationToken, ValueTask> onTextDelta,
            CancellationToken cancellationToken)
        {
            foreach (var delta in deltas)
            {
                await onTextDelta(delta, cancellationToken);

                if (delta == deltas.First())
                {
                    await _completeFinalResponse.Task.WaitAsync(cancellationToken);
                }
            }

            return response;
        }

        public void MarkDeltaObservedByRuntime() =>
            _deltaObservedByRuntime.TrySetResult();

        public async Task WaitUntilDeltaObservedByRuntimeAsync() =>
            await _deltaObservedByRuntime.Task.WaitAsync(TimeSpan.FromSeconds(3));

        public void CompleteFinalResponse() =>
            _completeFinalResponse.TrySetResult();
    }

    private sealed class EmptyToolRegistry : IAiToolRegistry
    {
        public Task<IReadOnlyCollection<AiToolDefinition>> GetAvailableToolsAsync(
            Guid organizationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<AiToolDefinition>>([]);
    }

    private sealed class ThrowingToolCallValidator : IAiToolCallValidator
    {
        public Task<ValidatedToolCall> ValidateAsync(
            AiRequestedToolCall requestedToolCall,
            IReadOnlyCollection<AiToolDefinition> availableTools,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingToolExecutionRouter : IToolExecutionRouter
    {
        public Task<ToolExecutionResult> ExecuteAsync(
            ValidatedToolCall toolCall,
            ConnectorExecutionContext executionContext,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
