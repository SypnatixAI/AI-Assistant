using System.Text.Json;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Messages;

public sealed class MicrosoftAgentRuntimeTests
{
    [Theory, AutoDomainData]
    public async Task Given_HistoryAndCurrentMessage_When_RunAsync_Then_SendsConversationToNativeChatClient(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        processing = processing with
        {
            UserMessage = "Et maintenant ?",
            ConversationHistory =
            [
                new AiConversationMessage(AiConversationRole.User, "Parle-moi du projet."),
                new AiConversationMessage(AiConversationRole.Assistant, "Que veux-tu savoir ?")
            ]
        };
        var chatClient = new SequenceChatClient([CreateTextResponse("Voici la suite.")]);
        var factory = new StubAgentChatClientFactory(chatClient);
        var runtime = CreateRuntime(factory, new EmptyToolRegistry());

        // When
        await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        var messages = Assert.Single(chatClient.ReceivedMessages);
        Assert.Equal(3, messages.Count);
        Assert.Equal("Parle-moi du projet.", messages[0].Text);
        Assert.Equal("Que veux-tu savoir ?", messages[1].Text);
        Assert.Equal("Et maintenant ?", messages[2].Text);
        Assert.Equal(selectedModel, Assert.Single(factory.SelectedModels));
    }

    [Theory, AutoDomainData]
    public async Task Given_DirectAnswer_When_RunAsync_Then_ReturnsNativeAgentResult(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var chatClient = new SequenceChatClient([
            CreateTextResponse("Bonjour.", inputTokens: 11, outputTokens: 4)
        ]);
        var runtime = CreateRuntime(
            new StubAgentChatClientFactory(chatClient),
            new EmptyToolRegistry());

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal("Bonjour.", result.Content);
        Assert.Empty(result.Citations);
        Assert.Empty(result.Warnings);
        Assert.Equal(11, result.Usage.InputTokens);
        Assert.Equal(4, result.Usage.OutputTokens);
        Assert.Equal(1, result.Usage.ModelCallCount);
        Assert.Equal(0, result.Usage.ToolCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoAuthorizedEnterpriseSearch_When_RunAsync_Then_ExposesNoTool(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var chatClient = new SequenceChatClient([CreateTextResponse("Réponse directe.")]);
        var runtime = CreateRuntime(
            new StubAgentChatClientFactory(chatClient),
            new EmptyToolRegistry());

        // When
        await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        var options = Assert.Single(chatClient.ReceivedOptions);
        Assert.True(options?.Tools is null || options.Tools.Count == 0);
    }

    [Theory, AutoDomainData]
    public void Given_AuthorizedEnterpriseSearch_When_CreateFunction_Then_UsesAuthorizedInputSchema(
        ConnectorExecutionContext executionContext)
    {
        // Given
        var tool = new EnterpriseSearchAgentTool(
            CreateAuthorizedEnterpriseSearchTool(),
            executionContext,
            new ThrowingToolCallValidator(),
            new ThrowingToolExecutionRouter());

        // When
        var function = tool.CreateFunction();

        // Then
        AssertEnterpriseSearchSchema(function.JsonSchema);
    }

    [Theory, AutoDomainData]
    public async Task Given_AuthorizedEnterpriseSearch_When_RunAsync_Then_FrameworkExecutesToolAndReturnsEvidence(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var evidence = CreateEvidence("internal-evidence");
        var chatClient = new SequenceChatClient([
            CreateToolCallResponse("call-1", "information recherchée"),
            CreateTextResponse("Voici l'information interne.")
        ]);
        var validator = new RecordingToolCallValidator();
        var router = new RecordingToolExecutionRouter(
            ToolExecutionResult.Succeeded("internal-call", [evidence]));
        var runtime = CreateRuntime(
            new StubAgentChatClientFactory(chatClient),
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]),
            validator,
            router);

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal("Voici l'information interne.", result.Content);
        Assert.Equal([evidence], result.Citations);
        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Usage.ModelCallCount);
        Assert.Equal(1, result.Usage.ToolCallCount);
        Assert.Single(validator.ReceivedToolCalls);
        Assert.Single(router.ReceivedToolCalls);
        Assert.Equal(2, chatClient.ReceivedMessages.Count);
        Assert.Contains(
            chatClient.ReceivedMessages[1].SelectMany(message => message.Contents),
            content => content is FunctionResultContent);
    }

    [Theory, AutoDomainData]
    public async Task Given_EnterpriseSearchFailure_When_RunAsync_Then_ReturnsFailureWarningAfterToolExecution(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var chatClient = new SequenceChatClient([
            CreateToolCallResponse("call-1", "information interne"),
            CreateTextResponse("Les informations internes sont temporairement indisponibles.")
        ]);
        var warning = "Microsoft 365 could not be consulted.";
        var router = new RecordingToolExecutionRouter(
            ToolExecutionResult.Failed(
                "internal-call",
                ToolExecutionErrorCodes.ExecutorNotFound,
                [warning]));
        var runtime = CreateRuntime(
            new StubAgentChatClientFactory(chatClient),
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]),
            new RecordingToolCallValidator(),
            router);

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            CancellationToken.None);

        // Then
        Assert.Equal([warning], result.Warnings);
        Assert.Empty(result.Citations);
        Assert.Single(router.ReceivedToolCalls);
    }

    [Theory, AutoDomainData]
    public async Task Given_InvalidEnterpriseExecutionContext_When_RunAsync_Then_ExposesNoTool(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var chatClient = new SequenceChatClient([CreateTextResponse("Réponse directe.")]);
        var runtime = CreateRuntime(
            new StubAgentChatClientFactory(chatClient),
            new StubToolRegistry([CreateAuthorizedEnterpriseSearchTool()]));

        // When
        await runtime.RunAsync(
            new AgentTurnRequest(
                processing,
                new ConnectorExecutionContext(Guid.NewGuid(), Guid.NewGuid()),
                selectedModel),
            CancellationToken.None);

        // Then
        var options = Assert.Single(chatClient.ReceivedOptions);
        Assert.True(options?.Tools is null || options.Tools.Count == 0);
    }

    [Theory, AutoDomainData]
    public async Task Given_NativeStreamingAnswer_When_RunStreamingAsync_Then_ForwardsProviderTextDeltas(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var chatClient = new StreamingChatClient([
            new ChatResponseUpdate(ChatRole.Assistant, "Allô"),
            new ChatResponseUpdate(ChatRole.Assistant, " !"),
            CreateUsageUpdate(inputTokens: 9, outputTokens: 2)
        ]);
        var runtime = CreateRuntime(
            new StubAgentChatClientFactory(chatClient),
            new EmptyToolRegistry());
        var deltas = new List<string>();
        var callbacks = new AgentTurnStreamingCallbacks(
            (_, _) => ValueTask.CompletedTask,
            (delta, _) =>
            {
                deltas.Add(delta);
                return ValueTask.CompletedTask;
            });

        // When
        var result = await runtime.RunStreamingAsync(
            new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
            callbacks,
            CancellationToken.None);

        // Then
        Assert.Equal(["Allô", " !"], deltas);
        Assert.Equal("Allô !", result.Content);
        Assert.Equal(9, result.Usage.InputTokens);
        Assert.Equal(2, result.Usage.OutputTokens);
        Assert.Equal(1, result.Usage.ModelCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_TurnTimeout_When_RunAsync_Then_CancelsNativeChatClient(
        StartedMessageProcessing processing)
    {
        // Given
        var selectedModel = CreateSelectedModel();
        var chatClient = new BlockingChatClient();
        var runtime = CreateRuntime(
            new StubAgentChatClientFactory(chatClient),
            new EmptyToolRegistry(),
            options: CreateOrchestrationOptions(maximumExecutionTimeSeconds: 1));

        // When
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runtime.RunAsync(
                new AgentTurnRequest(processing, CreateValidExecutionContext(), selectedModel),
                CancellationToken.None));

        // Then
        Assert.True(chatClient.ReceivedCancellationToken.IsCancellationRequested);
    }

    private static MicrosoftAgentRuntime CreateRuntime(
        IAgentChatClientFactory chatClientFactory,
        IAiToolRegistry toolRegistry,
        IAiToolCallValidator? toolCallValidator = null,
        IToolExecutionRouter? toolExecutionRouter = null,
        MessageOrchestrationOptions? options = null) =>
        new(
            chatClientFactory,
            toolRegistry,
            toolCallValidator ?? new ThrowingToolCallValidator(),
            toolExecutionRouter ?? new ThrowingToolExecutionRouter(),
            new ToolCallFingerprintGenerator(),
            Options.Create(options ?? CreateOrchestrationOptions()),
            NullLoggerFactory.Instance);

    private static SelectedAiModel CreateSelectedModel() =>
        new("OpenAI", "gpt-test");

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
            "Search authorized enterprise information.",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string" },
                    sourceTypes = new { anyOf = new object[] { new { type = "array", items = new { type = "string" } }, new { type = "null" } } },
                    dateFrom = new { anyOf = new object[] { new { type = "string" }, new { type = "null" } } },
                    dateTo = new { anyOf = new object[] { new { type = "string" }, new { type = "null" } } }
                },
                required = new[] { "query", "sourceTypes", "dateFrom", "dateTo" },
                additionalProperties = false
            }));

    private static ChatResponse CreateTextResponse(
        string text,
        int inputTokens = 10,
        int outputTokens = 5) =>
        new(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = CreateUsage(inputTokens, outputTokens)
        };

    private static ChatResponse CreateToolCallResponse(string callId, string query) =>
        new(new ChatMessage(
            ChatRole.Assistant,
            [
                new FunctionCallContent(
                    callId,
                    "EnterpriseSearch",
                    new Dictionary<string, object?>
                    {
                        ["query"] = query,
                        ["sourceTypes"] = null,
                        ["dateFrom"] = null,
                        ["dateTo"] = null
                    })
            ]))
        {
            Usage = CreateUsage(12, 4)
        };

    private static ChatResponseUpdate CreateUsageUpdate(
        int inputTokens,
        int outputTokens) =>
        new()
        {
            Contents = [new UsageContent(CreateUsage(inputTokens, outputTokens))]
        };

    private static UsageDetails CreateUsage(int inputTokens, int outputTokens) =>
        new()
        {
            InputTokenCount = inputTokens,
            OutputTokenCount = outputTokens,
            TotalTokenCount = inputTokens + outputTokens
        };

    private static RetrievedEvidence CreateEvidence(string evidenceId) =>
        new(
            evidenceId,
            "SharePoint",
            "Document interne",
            "Contenu autorisé.",
            "sharepoint://document",
            Url: null,
            OccurredAt: null);

    private static MessageOrchestrationOptions CreateOrchestrationOptions(
        int maximumExecutionTimeSeconds = 30) =>
        new()
        {
            MaximumExecutionTimeSeconds = maximumExecutionTimeSeconds,
            MaximumToolCalls = 4,
            MaximumModelTokens = 12_000,
            MaximumEstimatedCost = 1,
            RetrievalCandidateLimit = 10,
            FinalEvidenceLimit = 5,
            MaximumContextSize = 30_000,
            MaximumRepeatedToolCalls = 2,
            MaximumParallelToolCalls = 2
        };

    private static void AssertEnterpriseSearchSchema(JsonElement schema)
    {
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);

        var properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("query", out _));
        Assert.True(properties.TryGetProperty("sourceTypes", out _));
        Assert.True(properties.TryGetProperty("dateFrom", out _));
        Assert.True(properties.TryGetProperty("dateTo", out _));
    }

    private sealed class StubAgentChatClientFactory(IChatClient chatClient)
        : IAgentChatClientFactory
    {
        public List<SelectedAiModel> SelectedModels { get; } = [];

        public IChatClient Create(SelectedAiModel selectedModel)
        {
            SelectedModels.Add(selectedModel);
            return chatClient;
        }
    }

    private sealed class SequenceChatClient(
        IReadOnlyCollection<ChatResponse> responses) : IChatClient
    {
        private readonly Queue<ChatResponse> _responses = new(responses);

        public List<IReadOnlyList<ChatMessage>> ReceivedMessages { get; } = [];

        public List<ChatOptions?> ReceivedOptions { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedMessages.Add(messages.ToArray());
            ReceivedOptions.Add(options);

            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("No chat response was configured.");
            }

            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var content in response.Messages.SelectMany(message => message.Contents))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, [content]);
            }

            if (response.Usage is not null)
            {
                yield return new ChatResponseUpdate
                {
                    Contents = [new UsageContent(response.Usage)]
                };
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class StreamingChatClient(
        IReadOnlyCollection<ChatResponseUpdate> updates) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
                await Task.Yield();
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class BlockingChatClient : IChatClient
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Expected cancellation.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
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

        public Task<ToolExecutionResult> ExecuteAsync(
            ValidatedToolCall toolCall,
            ConnectorExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedToolCalls.Add(toolCall);
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
