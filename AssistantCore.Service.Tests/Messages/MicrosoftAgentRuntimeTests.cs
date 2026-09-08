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
using AssistantCore.Service.Application.Services.Messages.Orchestration;
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
    public void Given_AuthorizedEnterpriseSearchTool_When_CreateFunction_Then_UsesAuthorizedToolInputSchema(
        ConnectorExecutionContext executionContext)
    {
        // Given
        var enterpriseSearchTool = new EnterpriseSearchAgentTool(
            CreateAuthorizedEnterpriseSearchTool(),
            executionContext,
            new ThrowingToolCallValidator(),
            new ThrowingToolExecutionRouter());

        // When
        var function = enterpriseSearchTool.CreateFunction();

        // Then
        AssertEnterpriseSearchSchema(function.JsonSchema);
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
                CreateToolCallResponse(
                    "agent-call-1",
                    "EnterpriseSearch",
                    new EnterpriseSearchToolCallArguments(
                        "code projet Atlas",
                        ["sharepoint"],
                        "2026-01-01",
                        "2026-12-31")),
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
        AssertEnterpriseSearchSchema(exposedTool.InputSchema);
        var secondRequest = provider.ReceivedRequests[1];
        var modelVisibleToolResult = Assert.Single(secondRequest.ToolResults);
        Assert.Equal([evidence], modelVisibleToolResult.Evidence);
        var validatedCall = Assert.Single(validator.ReceivedToolCalls);
        Assert.Equal(AiToolNames.SearchMicrosoft365, validatedCall.ToolName);
        Assert.Equal("code projet Atlas", validatedCall.Arguments.GetProperty("query").GetString());
        Assert.Equal("sharepoint", validatedCall.Arguments.GetProperty("sourceTypes")[0].GetString());
        Assert.Equal("2026-01-01", validatedCall.Arguments.GetProperty("dateFrom").GetString());
        Assert.Equal("2026-12-31", validatedCall.Arguments.GetProperty("dateTo").GetString());
        var routedCall = Assert.Single(router.ReceivedToolCalls);
        Assert.Equal(AiToolNames.SearchMicrosoft365, routedCall.ToolName);
        var routedContext = Assert.Single(router.ReceivedContexts);
        Assert.Null(routedContext.Budget);
    }

    [Theory, AutoDomainData]
    public async Task Given_EnterpriseSearchCallsExceedMaximumToolCalls_When_RunAsync_Then_StopsFunctionCallLoop(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new SequenceAiModelProvider(
            selectedModel.Provider,
            [
                CreateToolCallResponse("agent-call-1", "EnterpriseSearch", "code projet Atlas"),
                CreateToolCallResponse("agent-call-2", "EnterpriseSearch", "code projet Orion"),
                CreateResponse("Ce message ne devrait pas etre demande.")
            ]);
        var router = new RecordingToolExecutionRouter(
            ToolExecutionResult.Succeeded("internal-call", [CreateEvidence("atlas-code")]));
        var runtime = CreateRuntime(
            [provider],
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]),
            new RecordingToolCallValidator(),
            router,
            CreateOrchestrationOptions(
                maximumToolCalls: 1,
                maximumRepeatedToolCalls: 5));

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal(2, provider.ReceivedRequests.Count);
        Assert.Single(router.ReceivedToolCalls);
        Assert.Equal(1, result.Usage.ToolCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_EnterpriseSearchRepeatsSameArgumentsBeyondMaximum_When_RunAsync_Then_StopsFunctionCallLoop(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new SequenceAiModelProvider(
            selectedModel.Provider,
            [
                CreateToolCallResponse("agent-call-1", "EnterpriseSearch", "code projet Atlas"),
                CreateToolCallResponse("agent-call-2", "EnterpriseSearch", "code projet Atlas"),
                CreateResponse("Ce message ne devrait pas etre demande.")
            ]);
        var router = new RecordingToolExecutionRouter(
            ToolExecutionResult.Succeeded("internal-call", [CreateEvidence("atlas-code")]));
        var runtime = CreateRuntime(
            [provider],
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]),
            new RecordingToolCallValidator(),
            router,
            CreateOrchestrationOptions(
                maximumToolCalls: 5,
                maximumRepeatedToolCalls: 1));

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal(2, provider.ReceivedRequests.Count);
        Assert.Single(router.ReceivedToolCalls);
        Assert.Equal(1, result.Usage.ToolCallCount);
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
    public async Task Given_AStreamingProvider_When_RunStreamingAsync_Then_ForwardsOnlyMappedFinalAnswer(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new ControlledStreamingAiModelProvider(
            selectedModel.Provider,
            CreateResponse("Le projet", inputTokens: 21, outputTokens: 6),
            ["{\"decision\":\"answer\",", "\"answer\":\"Le projet\"}"]);
        var runtime = CreateRuntime(provider);
        var receivedDeltas = new List<string>();
        var callbacks = new AgentTurnStreamingCallbacks(
            (_, _) => ValueTask.CompletedTask,
            (delta, _) =>
            {
                receivedDeltas.Add(delta);
                return ValueTask.CompletedTask;
            });

        // When
        var result = await runtime.RunStreamingAsync(
            new AgentTurnRequest(processing, executionContext, selectedModel),
            callbacks,
            CancellationToken.None);

        // Then
        Assert.Equal(["Le projet"], receivedDeltas);
        Assert.DoesNotContain(receivedDeltas, delta => delta.Contains("\"decision\"", StringComparison.Ordinal));
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

    [Theory, AutoDomainData]
    public async Task Given_TurnExceedsMaximumExecutionTimeSeconds_When_RunAsync_Then_CancelsAgentRun(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var provider = new BlockingAiModelProvider(selectedModel.Provider);
        var runtime = CreateRuntime(
            [provider],
            new EmptyToolRegistry(),
            options: CreateOrchestrationOptions(maximumExecutionTimeSeconds: 1));

        // When
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runtime.RunAsync(
                new AgentTurnRequest(processing, executionContext, selectedModel),
                CancellationToken.None));

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
        IToolExecutionRouter? toolExecutionRouter = null,
        MessageOrchestrationOptions? options = null) =>
        new(
            providers,
            toolRegistry,
            toolCallValidator ?? new ThrowingToolCallValidator(),
            toolExecutionRouter ?? new ThrowingToolExecutionRouter(),
            new ToolCallFingerprintGenerator(),
            Options.Create(options ?? CreateOrchestrationOptions()),
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
                },
                required = new[] { "query", "sourceTypes", "dateFrom", "dateTo" },
                additionalProperties = false
            }));

    private static MessageOrchestrationOptions CreateOrchestrationOptions(
        int maximumExecutionTimeSeconds = 30,
        int maximumToolCalls = 4,
        int maximumRepeatedToolCalls = 2) =>
        new()
        {
            MaximumExecutionTimeSeconds = maximumExecutionTimeSeconds,
            MaximumToolCalls = maximumToolCalls,
            MaximumModelTokens = 12_000,
            MaximumEstimatedCost = 1,
            RetrievalCandidateLimit = 10,
            FinalEvidenceLimit = 5,
            MaximumContextSize = 30_000,
            MaximumRepeatedToolCalls = maximumRepeatedToolCalls,
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
        CreateToolCallResponse(
            callId,
            toolName,
            new EnterpriseSearchToolCallArguments(
                query,
                SourceTypes: null,
                DateFrom: null,
                DateTo: null));

    private static AiModelResponse CreateToolCallResponse(
        string callId,
        string toolName,
        EnterpriseSearchToolCallArguments arguments) =>
        new(
            new AiModelDecision(
                AiModelDecisionType.UseTools,
                "The model requested enterprise search.",
                ToolCalls:
                [
                    new AiRequestedToolCall(
                        callId,
                        toolName,
                        JsonSerializer.SerializeToElement(new
                        {
                            query = arguments.Query,
                            sourceTypes = arguments.SourceTypes,
                            dateFrom = arguments.DateFrom,
                            dateTo = arguments.DateTo
                        }))
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

    private static void AssertEnterpriseSearchSchema(JsonElement schema)
    {
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);

        var properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("query", out _));
        Assert.True(properties.TryGetProperty("sourceTypes", out _));
        Assert.True(properties.TryGetProperty("dateFrom", out _));
        Assert.True(properties.TryGetProperty("dateTo", out _));

        var required = schema.GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString())
            .ToArray();
        Assert.Equal(["query", "sourceTypes", "dateFrom", "dateTo"], required!);
    }

    private sealed record EnterpriseSearchToolCallArguments(
        string Query,
        IReadOnlyCollection<string>? SourceTypes,
        string? DateFrom,
        string? DateTo);

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
        public string ProviderName => providerName;

        public Task<AiModelResponse> GetNextActionAsync(
            AiModelRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }

        public async Task<AiModelResponse> GetNextActionStreamingAsync(
            AiModelRequest request,
            Func<string, CancellationToken, ValueTask> onTextDelta,
            CancellationToken cancellationToken)
        {
            foreach (var delta in deltas)
            {
                await onTextDelta(delta, cancellationToken);
            }

            return response;
        }
    }

    private sealed class BlockingAiModelProvider(string providerName) : IAiModelProvider
    {
        public string ProviderName => providerName;

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public async Task<AiModelResponse> GetNextActionAsync(
            AiModelRequest request,
            CancellationToken cancellationToken)
        {
            ReceivedCancellationToken = cancellationToken;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            throw new InvalidOperationException("The blocking provider should only complete by cancellation.");
        }

        public Task<AiModelResponse> GetNextActionStreamingAsync(
            AiModelRequest request,
            Func<string, CancellationToken, ValueTask> onTextDelta,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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
