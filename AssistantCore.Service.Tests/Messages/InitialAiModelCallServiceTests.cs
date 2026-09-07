using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages;
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
    [InlineAutoDomainData(AiModelDecisionType.AskClarification)]
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
        var normalizedInstructions = NormalizeWhitespace(request.Instructions);
        Assert.Same(state.SelectedModel, request.Model);
        Assert.Equal(state.Question, request.UserMessage);
        Assert.Equal(state.ConversationHistory, request.ConversationHistory);
        Assert.Equal(state.AllowedTools, request.AllowedTools);
        Assert.Empty(request.RequestedToolCalls);
        Assert.Empty(request.ToolResults);
        Assert.Contains("Return \"askClarification\"", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Return \"cannotAnswer\"", request.Instructions, StringComparison.Ordinal);
        Assert.Contains(
            "never disclose internal implementation details",
            request.Instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "even when the user explicitly requests them",
            request.Instructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Never include evidence identifiers in answer",
            request.Instructions,
            StringComparison.Ordinal);
        Assert.Contains("language of the user's current message", normalizedInstructions, StringComparison.Ordinal);
        Assert.Contains("Interpret the current message in its conversation context", normalizedInstructions, StringComparison.Ordinal);
        Assert.Contains("reconstruct the complete request", normalizedInstructions, StringComparison.Ordinal);
        Assert.Contains("fulfill that offer directly", normalizedInstructions, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_KnowledgeRouting_When_RequestNextActionAsync_Then_SendsGeneralAndEnterpriseRules(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var provider = new RecordingAiModelProvider(
            "OpenAI",
            CreateResponse(AiModelDecisionType.Answer));
        var state = CreateState(processing, startedAtUtc);
        var service = new AiModelTurnService(
            [provider],
            new StubTimeProvider(startedAtUtc.AddSeconds(1)));

        // When
        await service.RequestNextActionAsync(state, CancellationToken.None);

        // Then
        var request = Assert.IsType<AiModelRequest>(provider.ReceivedRequest);
        var normalizedInstructions = NormalizeWhitespace(request.Instructions);
        Assert.Contains(
            "you may return \"answer\" directly from general model knowledge without calling a tool",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not call enterprise tools merely to support general knowledge",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Never infer, complete, or replace enterprise information with general model knowledge",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "including when no appropriate tool is available",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "not from a rigid keyword rule",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "The user does not need to explicitly say \"our\", \"my company\", or the organization's name",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "first try the available enterprise retrieval tools with those terms",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Use clarification only after retrieval when the evidence is missing, weak, or still ambiguous",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Use general model knowledge directly only when the request is clearly general",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Treat them as distinct unless the available evidence explicitly establishes a relationship",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Distinguish dates that describe the requested business period",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "If a source gives a day and month without a year, do not infer or add a year",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "For requests requiring an aggregation, ratio, comparison, or other derived result",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not assume missing values or invent a calculation rule",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "For financial comparisons across multiple documents, keep each document's scope and provenance separate",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not combine a total, provision, margin, threshold, or adjusted result from one document",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Verify arithmetic before concluding",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "If qualitative immediacy and quantitative size point to different risks",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Normalize each candidate risk before concluding",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "compute the missing margin amount from the relevant revenue and threshold",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not dismiss a margin issue as non-immediate without comparing that quantified shortfall",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "When break-even is part of the requested comparison, search for and use fixed costs",
            normalizedInstructions,
            StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_GroundednessReformulationRequired_When_RequestNextActionAsync_Then_ReusesEvidenceWithoutTools(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var provider = new RecordingAiModelProvider(
            "OpenAI",
            CreateResponse(AiModelDecisionType.Answer));
        var state = CreateState(processing, startedAtUtc);
        Assert.True(state.TryRequireGroundednessReformulation(3));
        var service = new AiModelTurnService(
            [provider],
            new StubTimeProvider(startedAtUtc.AddSeconds(1)));

        // When
        await service.RequestNextActionAsync(state, CancellationToken.None);

        // Then
        var request = Assert.IsType<AiModelRequest>(provider.ReceivedRequest);
        var normalizedInstructions = NormalizeWhitespace(request.Instructions);
        Assert.Empty(request.AllowedTools);
        Assert.Contains(
            "preserve partial dates exactly and never add a missing year, month, or day",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "return exactly one concise factual sentence",
            normalizedInstructions,
            StringComparison.Ordinal);
        Assert.Contains(
            "successful tool results already collected",
            normalizedInstructions,
            StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_CollectedEvidenceExceedsTheFinalLimit_When_RequestNextActionAsync_Then_SendsOnlyRetainedToolEvidence(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var provider = new RecordingAiModelProvider(
            "OpenAI",
            CreateResponse(AiModelDecisionType.Answer));
        var state = CreateState(processing, startedAtUtc);
        var evidence = Enumerable.Range(1, 10)
            .Select(index => new RetrievedEvidence(
                $"evidence-{index}",
                "Microsoft365",
                $"Title {index}",
                $"Content {index}",
                $"reference-{index}",
                null,
                null,
                index))
            .ToArray();
        state.RecordToolResults([ToolExecutionResult.Succeeded("tool-call", evidence)]);
        var service = new AiModelTurnService(
            [provider],
            new StubTimeProvider(startedAtUtc.AddSeconds(1)));

        // When
        await service.RequestNextActionAsync(state, CancellationToken.None);

        // Then
        var request = Assert.IsType<AiModelRequest>(provider.ReceivedRequest);
        var result = Assert.Single(request.ToolResults);
        Assert.Equal(
            Enumerable.Range(3, 8).Reverse().Select(value => $"evidence-{value}"),
            result.Evidence.Select(item => item.EvidenceId));
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
            RetrievalCandidateLimit: 20,
            FinalEvidenceLimit: 8,
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

    private static string NormalizeWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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

        public Task<AiModelResponse> GetNextActionStreamingAsync(
            AiModelRequest request,
            Func<string, CancellationToken, ValueTask> onTextDelta,
            CancellationToken cancellationToken) =>
            GetNextActionAsync(request, cancellationToken);
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
