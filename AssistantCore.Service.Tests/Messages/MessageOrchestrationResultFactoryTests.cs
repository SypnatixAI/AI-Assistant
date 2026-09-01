using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Evidence;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Orchestration;

namespace AssistantCore.Service.Tests.Messages;

public sealed class OrchestrationResultBuilderTests
{
    [Theory, AutoDomainData]
    public void Given_PartialSourceFailureAndKnownEvidence_When_Build_Then_ReturnsSupportedAnswerAndAggregatedUsage(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        var evidence = CreateEvidence("evidence-known");
        state.Budget.RecordModelUsage(new AiModelUsage(10, 4, 1, 0, 0.10m), startedAtUtc);
        state.Budget.RecordModelUsage(new AiModelUsage(6, 3, 1, 0, 0.05m), startedAtUtc);
        state.AcceptToolCalls(
            CreateToolCalls(2),
            ["signature-one", "signature-two"],
            startedAtUtc);
        state.RecordToolResults(
        [
            ToolExecutionResult.Succeeded("call-1", [evidence]),
            ToolExecutionResult.Failed("call-2", "SOURCE_UNAVAILABLE", ["CRM unavailable."])
        ]);
        var response = CreateResponse(
            AiModelDecisionType.Answer,
            "  Supported answer.  ",
            [evidence.EvidenceId]);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var result = builder.Build(state, response);

        // Then
        Assert.Equal("Supported answer.", result.Answer);
        Assert.Equal(state.SelectedModel.ModelName, result.ModelName);
        Assert.Equal([evidence], result.CitedEvidence);
        Assert.Equal(["CRM unavailable."], result.Warnings);
        Assert.Equal(16, result.Usage.InputTokens);
        Assert.Equal(7, result.Usage.OutputTokens);
        Assert.Equal(2, result.Usage.ModelCallCount);
        Assert.Equal(2, result.Usage.ToolCallCount);
        Assert.Equal(0.15m, result.Usage.EstimatedCost);
    }

    [Theory, AutoDomainData]
    public void Given_AllSourcesFailedWithoutEvidence_When_Build_Then_ThrowsControlledError(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        state.RecordToolResults(
        [
            ToolExecutionResult.Failed("call-1", "SOURCE_UNAVAILABLE", ["Source unavailable."])
        ]);
        var response = CreateResponse(
            AiModelDecisionType.InsufficientInformation,
            Answer: "The sources are temporarily unavailable.",
            CitedEvidenceIds: []);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var exception = Record.Exception(() => builder.Build(state, response));

        // Then
        Assert.IsType<ExternalSourcesUnavailableException>(exception);
    }

    [Theory, AutoDomainData]
    public void Given_AnEmptyAnswer_When_Build_Then_RejectsTheProviderResponse(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        var response = CreateResponse(
            AiModelDecisionType.Answer,
            "   ",
            []);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var exception = Record.Exception(() => builder.Build(state, response));

        // Then
        Assert.IsType<AiProviderInvalidResponseException>(exception);
    }

    [Theory, AutoDomainData]
    public void Given_AGeneralAnswerWithoutEvidence_When_Build_Then_ReturnsTheDirectAnswer(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc,
        string expectedAnswer)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        var response = CreateResponse(
            AiModelDecisionType.Answer,
            expectedAnswer,
            CitedEvidenceIds: []);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var result = builder.Build(state, response);

        // Then
        Assert.Equal(expectedAnswer, result.Answer);
        Assert.Empty(result.CitedEvidence);
    }

    [Theory, AutoDomainData]
    public void Given_InsufficientInformation_When_Build_Then_ReturnsAnExplicitSafeAnswer(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc,
        string expectedAnswer)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        var response = CreateResponse(
            AiModelDecisionType.InsufficientInformation,
            expectedAnswer,
            CitedEvidenceIds: []);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var result = builder.Build(state, response);

        // Then
        Assert.Equal(expectedAnswer, result.Answer);
        Assert.Empty(result.CitedEvidence);
    }

    [Theory, AutoDomainData]
    public void Given_AnAnswerContainingAnEvidenceIdentifier_When_Build_Then_RemovesTheInternalIdentifier(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        var evidence = CreateEvidence("reference-for-answer");
        state.RecordToolResults(
        [
            ToolExecutionResult.Succeeded("call-1", [evidence])
        ]);
        var response = CreateResponse(
            AiModelDecisionType.Answer,
            $"Supported answer. [{evidence.EvidenceId}]",
            [evidence.EvidenceId]);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var result = builder.Build(state, response);

        // Then
        Assert.Equal("Supported answer.", result.Answer);
        Assert.Equal([evidence], result.CitedEvidence);
    }

    [Theory, AutoDomainData]
    public void Given_AnUnknownEvidenceIdentifier_When_Build_Then_RejectsTheProviderResponse(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc,
        string unknownEvidenceId)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        var response = CreateResponse(
            AiModelDecisionType.Answer,
            "Supported answer.",
            [unknownEvidenceId]);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var exception = Record.Exception(() => builder.Build(state, response));

        // Then
        Assert.IsType<AiProviderInvalidResponseException>(exception);
    }

    [Theory, AutoDomainData]
    public void Given_RetrievedEvidenceAndAnUncitedAnswer_When_Build_Then_RejectsTheProviderResponse(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var state = CreateState(processing, startedAtUtc);
        state.RecordToolResults(
        [
            ToolExecutionResult.Succeeded("call-1", [CreateEvidence("evidence-known")])
        ]);
        var response = CreateResponse(
            AiModelDecisionType.Answer,
            "Uncited answer.",
            CitedEvidenceIds: []);
        var builder = new OrchestrationResultBuilder(new EvidenceCitationResolver());

        // When
        var exception = Record.Exception(() => builder.Build(state, response));

        // Then
        Assert.IsType<AiProviderInvalidResponseException>(exception);
    }

    private static MessageOrchestrationState CreateState(
        StartedMessageProcessing processing,
        DateTimeOffset startedAtUtc) =>
        MessageOrchestrationState.Start(
            processing,
            new SelectedAiModel("OpenAI", "gpt-test"),
            [],
            [
                new AiToolDefinition(
                    AiToolNames.SearchInternalData,
                    "Search internal data.",
                    JsonSerializer.SerializeToElement(new { type = "object" }))
            ],
            new OrchestrationExecutionLimits(
                TimeSpan.FromMinutes(2),
                MaximumToolCalls: 5,
                MaximumModelTokens: 1_000,
                MaximumEstimatedCost: 1m,
                RetrievalCandidateLimit: 20,
                FinalEvidenceLimit: 8,
                MaximumContextSize: 1_000,
                MaximumRepeatedToolCalls: 1),
            startedAtUtc);

    private static AiModelResponse CreateResponse(
        AiModelDecisionType decisionType,
        string? Answer,
        IReadOnlyCollection<string> CitedEvidenceIds) =>
        new(
            new AiModelDecision(
                decisionType,
                "Terminal response.",
                [],
                Answer,
                CitedEvidenceIds),
            new AiModelUsage(0, 0, 0, 0, EstimatedCost: null));

    private static IReadOnlyCollection<AiRequestedToolCall> CreateToolCalls(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new AiRequestedToolCall(
                $"call-{index}",
                AiToolNames.SearchInternalData,
                JsonSerializer.SerializeToElement(new { query = index })))
            .ToArray();

    private static RetrievedEvidence CreateEvidence(string reference) =>
        Assert.Single(new EvidenceNormalizer().Normalize(
            [new EvidenceCandidate(
                "Internal",
                "Title",
                "Content",
                reference,
                Url: null,
                OccurredAt: null,
                RelevanceScore: null)],
            new EvidenceNormalizationOptions(
                MaximumContentLength: 1_000,
                MaximumResults: 1)));
}
