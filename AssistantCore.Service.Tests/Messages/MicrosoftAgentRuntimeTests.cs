using System.Text.Json;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages;
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
    public async Task Given_NoAuthorizedEnterpriseSearchTool_When_RunAsync_Then_ExposesNoToolToTheModel(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new RecordingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Bonjour."));
        var runtime = CreateRuntime(
            [provider],
            new StubToolRegistry([]));

        // When
        await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        var request = Assert.Single(provider.ReceivedRequests);
        Assert.Empty(request.AllowedTools);
    }

    [Theory, AutoDomainData]
    public async Task Given_AuthorizedEnterpriseSearchTool_When_ModelRequestsIt_Then_ExecutesItAndReturnsFinalAnswer(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var evidence = CreateEvidence("atlas-code");
        var toolResult = ToolExecutionResult.Succeeded("internal-call", [evidence]);
        var provider = new SequenceAiModelProvider(
            selectedModel.Provider,
            [
                CreateToolCallResponse("agent-call-1", "EnterpriseSearch", "code projet Atlas"),
                CreateResponse("Le code du projet Atlas est AT-42.")
            ]);
        var validator = new RecordingToolCallValidator();
        var router = new RecordingToolExecutionRouter(toolResult);
        var runtime = CreateRuntime(
            [provider],
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]),
            validator,
            router);

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal("Le code du projet Atlas est AT-42.", result.Content);
        Assert.Equal(2, provider.ReceivedRequests.Count);
        var firstRequest = provider.ReceivedRequests[0];
        var exposedTool = Assert.Single(firstRequest.AllowedTools);
        Assert.Equal("EnterpriseSearch", exposedTool.Name);
        var secondRequest = provider.ReceivedRequests[1];
        var modelVisibleToolResult = Assert.Single(secondRequest.ToolResults);
        Assert.Equal([evidence], modelVisibleToolResult.Evidence);
        var validatedCall = Assert.Single(validator.ReceivedToolCalls);
        Assert.Equal(AiToolNames.SearchMicrosoft365, validatedCall.ToolName);
        Assert.Equal("code projet Atlas", validatedCall.Arguments.GetProperty("query").GetString());
        var routedCall = Assert.Single(router.ReceivedToolCalls);
        Assert.Equal(AiToolNames.SearchMicrosoft365, routedCall.ToolName);
    }

    [Theory, AutoDomainData]
    public async Task Given_EnterpriseSearchResultWithEvidence_When_RunAsync_Then_ReturnsCitedEvidence(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var evidence = CreateEvidence("atlas-code");
        var provider = new SequenceAiModelProvider(
            selectedModel.Provider,
            [
                CreateToolCallResponse("agent-call-1", "EnterpriseSearch", "code projet Atlas"),
                CreateResponse(
                    "Le code du projet Atlas est AT-42.",
                    citedEvidenceIds: [evidence.EvidenceId])
            ]);
        var runtime = CreateRuntime(
            [provider],
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]),
            new RecordingToolCallValidator(),
            new RecordingToolExecutionRouter(ToolExecutionResult.Succeeded("internal-call", [evidence])));

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal([evidence], result.Citations);
    }

    [Theory, AutoDomainData]
    public async Task Given_InvalidUserExecutionContext_When_RunAsync_Then_ExposesNoEnterpriseSearchTool(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new RecordingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Je ne peux pas confirmer cette information."));
        var runtime = CreateRuntime(
            [provider],
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]));

        // When
        await runtime.RunAsync(
            new AgentTurnRequest(
                processing,
                new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid()),
                selectedModel),
            CancellationToken.None);

        // Then
        var request = Assert.Single(provider.ReceivedRequests);
        Assert.Empty(request.AllowedTools);
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
        CreateRuntime(providers, new EmptyToolRegistry());

    private static MicrosoftAgentRuntime CreateRuntime(
        IReadOnlyCollection<IAiModelProvider> providers,
        IAiToolRegistry toolRegistry,
        IAiToolCallValidator? toolCallValidator = null,
        IToolExecutionRouter? toolExecutionRouter = null) =>
        new(
            providers,
            toolRegistry,
            toolCallValidator ?? new ThrowingToolCallValidator(),
            toolExecutionRouter ?? new ThrowingToolExecutionRouter(),
            Options.Create(CreateOrchestrationOptions()),
            TimeProvider.System,
            NullLoggerFactory.Instance);

    private static ConnectorExecutionContext CreateValidExecutionContext() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "tenant-id",
            Guid.NewGuid(),
            IdentityProvider.MicrosoftEntraId,
            UserEmail: "member@synaptix.local");

    private static AiToolDefinition CreateAuthorizedEnterpriseSearchTool() =>
        new(
            AiToolNames.SearchMicrosoft365,
            "Search Microsoft 365.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string" },
                    sourceTypes = new { type = "array", nullable = true },
                    dateFrom = new { type = "string", nullable = true },
                    dateTo = new { type = "string", nullable = true }
                }
            }));

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
        int outputTokens = 5,
        IReadOnlyCollection<string>? citedEvidenceIds = null) =>
        new(
            new AiModelDecision(
                AiModelDecisionType.Answer,
                "The model answered.",
                ToolCalls: [],
                answer,
                CitedEvidenceIds: citedEvidenceIds ?? []),
            new AiModelUsage(
                inputTokens,
                outputTokens,
                ModelCallCount: 1,
                ToolCallCount: 0,
                EstimatedCost: null));

    private static AiModelResponse CreateToolCallResponse(
        string callId,
        string toolName,
        string query) =>
        new(
            new AiModelDecision(
                AiModelDecisionType.UseTools,
                "The model requested enterprise search.",
                ToolCalls:
                [
                    new AiRequestedToolCall(
                        callId,
                        toolName,
                        JsonSerializer.SerializeToElement(new { query }))
                ],
                Answer: null,
                CitedEvidenceIds: []),
            new AiModelUsage(
                InputTokens: 12,
                OutputTokens: 4,
                ModelCallCount: 1,
                ToolCallCount: 1,
                EstimatedCost: null));

    private static RetrievedEvidence CreateEvidence(string evidenceId) =>
        new(
            evidenceId,
            "SharePoint",
            "Projet Atlas",
            "Le code du projet Atlas est AT-42.",
            "sharepoint://atlas",
            Url: null,
            OccurredAt: null);

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

    private sealed class SequenceAiModelProvider(
        string providerName,
        IReadOnlyCollection<AiModelResponse> responses) : IAiModelProvider
    {
        private readonly Queue<AiModelResponse> _responses = new(responses);

        public string ProviderName => providerName;

        public List<AiModelRequest> ReceivedRequests { get; } = [];

        public Task<AiModelResponse> GetNextActionAsync(
            AiModelRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedRequests.Add(request);

            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("No AI model response was configured for this request.");
            }

            return Task.FromResult(response);
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

    private sealed class StubToolRegistry(
        IReadOnlyCollection<AiToolDefinition> availableTools) : IAiToolRegistry
    {
        public Task<IReadOnlyCollection<AiToolDefinition>> GetAvailableToolsAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(availableTools);
        }
    }

    private sealed class RecordingToolCallValidator : IAiToolCallValidator
    {
        public List<AiRequestedToolCall> ReceivedToolCalls { get; } = [];

        public Task<ValidatedToolCall> ValidateAsync(
            AiRequestedToolCall requestedToolCall,
            IReadOnlyCollection<AiToolDefinition> availableTools,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedToolCalls.Add(requestedToolCall);

            return Task.FromResult(new ValidatedToolCall(
                requestedToolCall.CallId,
                requestedToolCall.ToolName,
                requestedToolCall.Arguments.Clone()));
        }
    }

    private sealed class RecordingToolExecutionRouter(
        ToolExecutionResult result) : IToolExecutionRouter
    {
        public List<ValidatedToolCall> ReceivedToolCalls { get; } = [];

        public List<ConnectorExecutionContext> ReceivedContexts { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(
            ValidatedToolCall toolCall,
            ConnectorExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedToolCalls.Add(toolCall);
            ReceivedContexts.Add(executionContext);

            return Task.FromResult(result);
        }
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
