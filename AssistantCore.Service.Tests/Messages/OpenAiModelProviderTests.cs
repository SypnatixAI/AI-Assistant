using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.AiModels.Adapters.OpenAI;
using AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;

namespace AssistantCore.Service.Tests.Messages;

public sealed class OpenAiModelProviderTests
{
    [Fact]
    public async Task Given_AValidAnswer_When_GetNextActionAsync_Then_ReturnsTheMappedDecisionAndUsage()
    {
        // Given
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromResult(new OpenAiResponsesResult(
                "response-001",
                """
                {
                  "decision": "answer",
                  "reason": "The available evidence answers the question.",
                  "answer": "Sales increased by eight percent.",
                  "progressMessage": "I found the relevant sales figures.",
                  "evidenceIds": ["source-001"]
                }
                """,
                [],
                InputTokens: 120,
                OutputTokens: 40))
        };
        var provider = CreateProvider(client);

        // When
        var result = await provider.GetNextActionAsync(CreateRequest(), CancellationToken.None);

        // Then
        Assert.Equal(AiModelDecisionType.Answer, result.Decision.Type);
        Assert.Equal("Sales increased by eight percent.", result.Decision.Answer);
        Assert.Equal("I found the relevant sales figures.", result.Decision.ProgressMessage);
        Assert.Equal(["source-001"], result.Decision.CitedEvidenceIds);
        Assert.Equal(120, result.Usage.InputTokens);
        Assert.Equal(40, result.Usage.OutputTokens);
        Assert.Equal(1, result.Usage.ModelCallCount);
        Assert.Equal("OpenAI", result.ContinuationContext?.Provider);
        Assert.Equal("response-001", result.ContinuationContext?.Token);
    }

    [Fact]
    public async Task Given_AFunctionCall_When_GetNextActionAsync_Then_ReturnsTheRequestedToolWithoutExecutingIt()
    {
        // Given
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromResult(new OpenAiResponsesResult(
                "response-002",
                """
                {
                  "decision": "answer",
                  "reason": "A source lookup is required.",
                  "answer": "Search in progress.",
                  "progressMessage": "Je consulte les données de vente.",
                  "evidenceIds": []
                }
                """,
                [new OpenAiToolCall("call-001", "query_erp", "{\"metric\":\"sales\"}")],
                InputTokens: 80,
                OutputTokens: 20))
        };
        var provider = CreateProvider(client);

        // When
        var result = await provider.GetNextActionAsync(CreateRequest(), CancellationToken.None);

        // Then
        Assert.Equal(AiModelDecisionType.UseTools, result.Decision.Type);
        var toolCall = Assert.Single(result.Decision.ToolCalls);
        Assert.Equal("call-001", toolCall.CallId);
        Assert.Equal("query_erp", toolCall.ToolName);
        Assert.Equal("sales", toolCall.Arguments.GetProperty("metric").GetString());
        Assert.Null(result.Decision.Answer);
        Assert.Equal("Je consulte les données de vente.", result.Decision.ProgressMessage);
        Assert.Equal(1, result.Usage.ToolCallCount);
    }

    [Fact]
    public async Task Given_MultipleFunctionCalls_When_GetNextActionAsync_Then_ReturnsEveryRequestedToolWithoutExecutingThem()
    {
        // Given
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromResult(new OpenAiResponsesResult(
                "response-003",
                string.Empty,
                [
                    new OpenAiToolCall("call-001", "query_erp", "{\"metric\":\"sales\"}"),
                    new OpenAiToolCall(
                        "call-002",
                        "search_microsoft_365",
                        "{\"query\":\"quarterly sales report\"}")
                ],
                InputTokens: 100,
                OutputTokens: 30))
        };
        var provider = CreateProvider(client);

        // When
        var result = await provider.GetNextActionAsync(CreateRequest(), CancellationToken.None);

        // Then
        Assert.Equal(AiModelDecisionType.UseTools, result.Decision.Type);
        Assert.Collection(
            result.Decision.ToolCalls,
            toolCall =>
            {
                Assert.Equal("call-001", toolCall.CallId);
                Assert.Equal("query_erp", toolCall.ToolName);
            },
            toolCall =>
            {
                Assert.Equal("call-002", toolCall.CallId);
                Assert.Equal("search_microsoft_365", toolCall.ToolName);
            });
        Assert.Equal(2, result.Usage.ToolCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACannotAnswerDecision_When_GetNextActionAsync_Then_ReturnsLocalizedInsufficientInformation(
        string responseId,
        string userMessage)
    {
        // Given
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromResult(new OpenAiResponsesResult(
                responseId,
                """
                {
                  "decision": "cannotAnswer",
                  "reason": "No available source contains the requested information.",
                  "answer": "{{userMessage}}",
                  "evidenceIds": []
                }
                """.Replace("{{userMessage}}", userMessage, StringComparison.Ordinal),
                [],
                InputTokens: 90,
                OutputTokens: 25))
        };
        var provider = CreateProvider(client);

        // When
        var result = await provider.GetNextActionAsync(CreateRequest(), CancellationToken.None);

        // Then
        Assert.Equal(AiModelDecisionType.InsufficientInformation, result.Decision.Type);
        Assert.Equal(userMessage, result.Decision.Answer);
        Assert.Empty(result.Decision.CitedEvidenceIds);
    }

    [Theory, AutoDomainData]
    public async Task Given_AClarificationDecision_When_GetNextActionAsync_Then_ReturnsTheQuestion(
        string responseId,
        string clarificationQuestion)
    {
        // Given
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromResult(new OpenAiResponsesResult(
                responseId,
                $$"""
                {
                  "decision": "askClarification",
                  "reason": "A material subject is missing.",
                  "answer": "{{clarificationQuestion}}",
                  "evidenceIds": []
                }
                """,
                [],
                InputTokens: 90,
                OutputTokens: 25))
        };
        var provider = CreateProvider(client);

        // When
        var result = await provider.GetNextActionAsync(CreateRequest(), CancellationToken.None);

        // Then
        Assert.Equal(AiModelDecisionType.AskClarification, result.Decision.Type);
        Assert.Equal(clarificationQuestion, result.Decision.Answer);
        Assert.Empty(result.Decision.CitedEvidenceIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"decision\":\"answer\",\"reason\":\"Missing answer\"}")]
    [InlineData("{\"decision\":\"cannotAnswer\",\"reason\":\"Missing answer\",\"answer\":null,\"evidenceIds\":[]}")]
    public async Task Given_AnInvalidResponse_When_GetNextActionAsync_Then_ThrowsControlledInvalidResponse(
        string outputText)
    {
        // Given
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromResult(new OpenAiResponsesResult(
                "response-005",
                outputText,
                [],
                InputTokens: 0,
                OutputTokens: 0))
        };
        var provider = CreateProvider(client);

        // When
        var exception = await Assert.ThrowsAsync<AiProviderInvalidResponseException>(() =>
            provider.GetNextActionAsync(CreateRequest(), CancellationToken.None));

        // Then
        Assert.Equal("OpenAI", exception.ProviderName);
        Assert.Equal("AI_PROVIDER_INVALID_RESPONSE", exception.TechnicalCode);
    }

    [Fact]
    public async Task Given_AClientCancellation_When_GetNextActionAsync_Then_PropagatesCancellation()
    {
        // Given
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, token) => Task.FromCanceled<OpenAiResponsesResult>(token)
        };
        var provider = CreateProvider(client);

        // When
        var exception = await Record.ExceptionAsync(() =>
            provider.GetNextActionAsync(CreateRequest(), cancellationSource.Token));

        // Then
        Assert.IsType<TaskCanceledException>(exception);
    }

    [Fact]
    public async Task Given_TheConfiguredTimeout_When_GetNextActionAsync_Then_ThrowsControlledTimeout()
    {
        // Given
        var client = new StubOpenAiResponsesClient
        {
            Handler = async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("The delay should have been cancelled.");
            }
        };
        var provider = CreateProvider(client, timeoutSeconds: 1);

        // When
        var exception = await Assert.ThrowsAsync<AiProviderTimeoutException>(() =>
            provider.GetNextActionAsync(CreateRequest(), CancellationToken.None));

        // Then
        Assert.Equal("AI_PROVIDER_TIMEOUT", exception.TechnicalCode);
    }

    [Fact]
    public async Task Given_AnOpenAiLimit_When_GetNextActionAsync_Then_ThrowsControlledLimit()
    {
        // Given
        var client = CreateFailingClient(StatusCodes.Status429TooManyRequests);
        var provider = CreateProvider(client);

        // When
        var exception = await Assert.ThrowsAsync<AiProviderLimitException>(() =>
            provider.GetNextActionAsync(CreateRequest(), CancellationToken.None));

        // Then
        Assert.Equal("AI_PROVIDER_LIMIT", exception.TechnicalCode);
    }

    [Theory]
    [InlineData(StatusCodes.Status500InternalServerError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public async Task Given_AnUnavailableOpenAiProvider_When_GetNextActionAsync_Then_ThrowsControlledUnavailable(
        int statusCode)
    {
        // Given
        var client = CreateFailingClient(statusCode);
        var provider = CreateProvider(client);

        // When
        var exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            provider.GetNextActionAsync(CreateRequest(), CancellationToken.None));

        // Then
        Assert.Equal("AI_PROVIDER_UNAVAILABLE", exception.TechnicalCode);
        Assert.Equal(statusCode, exception.ProviderStatusCode);
    }

    [Theory, AutoDomainData]
    public async Task Given_AStreamingAnswer_When_GetNextActionStreamingAsync_Then_ForwardsDeltasAndReturnsTheMappedDecision(
        string unused)
    {
        // Given
        _ = unused;
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromResult(new OpenAiResponsesResult(
                "response-stream-001",
                """
                {"decision":"answer","reason":"Supported.","answer":"Bonjour monde","progressMessage":"J’ai vérifié les sources.","evidenceIds":[]}
                """,
                [],
                InputTokens: 12,
                OutputTokens: 4)),
            StreamingHandler = async (_, onTextDelta, token) =>
            {
                await onTextDelta("Bonjour", token);
                await onTextDelta(" monde", token);
                return await Task.FromResult(new OpenAiResponsesResult(
                    "response-stream-001",
                    """
                    {"decision":"answer","reason":"Supported.","answer":"Bonjour monde","progressMessage":"J’ai vérifié les sources.","evidenceIds":[]}
                    """,
                    [],
                    InputTokens: 12,
                    OutputTokens: 4));
            }
        };
        var provider = CreateProvider(client);
        var deltas = new List<string>();

        // When
        var result = await provider.GetNextActionStreamingAsync(
            CreateRequest(),
            (delta, _) =>
            {
                deltas.Add(delta);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        // Then
        Assert.Equal(["Bonjour", " monde"], deltas);
        Assert.Equal(AiModelDecisionType.Answer, result.Decision.Type);
        Assert.Equal("Bonjour monde", result.Decision.Answer);
        Assert.Equal("J’ai vérifié les sources.", result.Decision.ProgressMessage);
    }

    [Theory, AutoDomainData]
    public async Task Given_AClientCancellation_When_GetNextActionStreamingAsync_Then_PropagatesCancellation(
        string unused)
    {
        // Given
        _ = unused;
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromCanceled<OpenAiResponsesResult>(cancellationSource.Token),
            StreamingHandler = (_, _, token) => Task.FromCanceled<OpenAiResponsesResult>(token)
        };
        var provider = CreateProvider(client);

        // When
        var exception = await Record.ExceptionAsync(() => provider.GetNextActionStreamingAsync(
            CreateRequest(),
            (_, _) => ValueTask.CompletedTask,
            cancellationSource.Token));

        // Then
        Assert.IsType<TaskCanceledException>(exception);
    }

    [Theory, AutoDomainData]
    public async Task Given_AStreamingTransportFailure_When_GetNextActionStreamingAsync_Then_ThrowsControlledUnavailable(
        string unused)
    {
        // Given
        _ = unused;
        var client = new StubOpenAiResponsesClient
        {
            Handler = (_, _) => Task.FromException<OpenAiResponsesResult>(
                new OpenAiTransportException(StatusCodes.Status503ServiceUnavailable)),
            StreamingHandler = (_, _, _) => Task.FromException<OpenAiResponsesResult>(
                new OpenAiTransportException(StatusCodes.Status503ServiceUnavailable))
        };
        var provider = CreateProvider(client);

        // When
        var exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            provider.GetNextActionStreamingAsync(
                CreateRequest(),
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None));

        // Then
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.ProviderStatusCode);
    }

    private static StubOpenAiResponsesClient CreateFailingClient(int statusCode) =>
        new()
        {
            Handler = (_, _) => Task.FromException<OpenAiResponsesResult>(
                new OpenAiTransportException(statusCode))
        };

    private static OpenAiModelProvider CreateProvider(
        IOpenAiResponsesClient client,
        int timeoutSeconds = 60)
    {
        return new OpenAiModelProvider(
            client,
            new OpenAiResponseMapper(),
            TimeSpan.FromSeconds(timeoutSeconds));
    }

    private static AiModelRequest CreateRequest()
    {
        return new AiModelRequest(
            new SelectedAiModel("OpenAI", "gpt-5.6-luna"),
            "Return a structured orchestration decision.",
            "What happened to sales?",
            [],
            [],
            [],
            []);
    }

    private sealed class StubOpenAiResponsesClient : IOpenAiResponsesClient
    {
        public required Func<AiModelRequest, CancellationToken, Task<OpenAiResponsesResult>> Handler
        {
            get;
            init;
        }

        public Func<
            AiModelRequest,
            Func<string, CancellationToken, ValueTask>,
            CancellationToken,
            Task<OpenAiResponsesResult>>? StreamingHandler { get; init; }

        public Task<OpenAiResponsesResult> CreateResponseAsync(
            AiModelRequest request,
            CancellationToken cancellationToken) =>
            Handler(request, cancellationToken);

        public Task<OpenAiResponsesResult> CreateResponseStreamingAsync(
            AiModelRequest request,
            Func<string, CancellationToken, ValueTask> onTextDelta,
            CancellationToken cancellationToken) =>
            (StreamingHandler ?? ((streamRequest, _, token) => Handler(streamRequest, token)))(
                request,
                onTextDelta,
                cancellationToken);
    }
}
