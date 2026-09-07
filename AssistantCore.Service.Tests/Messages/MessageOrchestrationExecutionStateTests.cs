using System.Text.Json;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class MessageOrchestrationStateTests
{
    [Theory, AutoDomainData]
    public void Given_ValidExecutionInputs_When_Start_Then_InitializesExecutionContextAndBudgets(
        Guid organizationId,
        Guid memberId,
        Guid conversationId,
        Guid userMessageId,
        string question,
        string provider,
        string modelName,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var processing = new StartedMessageProcessing(
            organizationId,
            memberId,
            conversationId,
            userMessageId,
            question);
        var selectedModel = new SelectedAiModel(provider, modelName);
        var executionContext = new ConnectorExecutionContext(
            organizationId,
            memberId,
            "tenant-id",
            Guid.NewGuid(),
            IdentityProvider.MicrosoftEntraId,
            UserEmail: "user@contoso.com");
        var conversationHistory = new[]
        {
            new AiConversationMessage(AiConversationRole.User, "Previous question"),
            new AiConversationMessage(AiConversationRole.Assistant, "Previous answer")
        };
        var tools = new List<AiToolDefinition>
        {
            new(
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

        // When
        var state = MessageOrchestrationState.Start(
            processing,
            executionContext,
            selectedModel,
            conversationHistory,
            tools,
            limits,
            startedAtUtc);

        // Then
        Assert.Same(processing, state.MessageProcessing);
        Assert.Equal(question, state.Question);
        Assert.Same(selectedModel, state.SelectedModel);
        Assert.Equal(conversationHistory, state.ConversationHistory);
        Assert.Equal(organizationId, state.ToolExecutionContext.OrganizationId);
        Assert.Equal(memberId, state.ToolExecutionContext.MemberId);
        Assert.Equal(executionContext.OrganizationId, state.ToolExecutionContext.OrganizationId);
        Assert.Equal(executionContext.MemberId, state.ToolExecutionContext.MemberId);
        Assert.Equal(executionContext.ExternalTenantId, state.ToolExecutionContext.ExternalTenantId);
        Assert.Equal(executionContext.EntraUserId, state.ToolExecutionContext.EntraUserId);
        Assert.Equal(executionContext.IdentityProvider, state.ToolExecutionContext.IdentityProvider);
        Assert.Equal(executionContext.UserEmail, state.ToolExecutionContext.UserEmail);
        Assert.Equal(limits.RetrievalCandidateLimit, state.ToolExecutionContext.RetrievalCandidateLimit);
        Assert.Same(limits, state.Budget.Limits);
        Assert.Equal(startedAtUtc, state.Budget.StartedAtUtc);
        Assert.Equal(
            startedAtUtc.Add(limits.MaximumExecutionTime),
            state.Budget.DeadlineUtc);
        Assert.Single(state.AllowedTools);
        Assert.Empty(state.CollectedEvidence);
        Assert.Empty(state.Warnings);
        Assert.Empty(state.RequestedToolCalls);
        Assert.Empty(state.ToolResults);
        Assert.Equal(TimeSpan.Zero, state.Budget.Usage.ExecutionTime);
        Assert.Equal(0, state.Budget.Usage.ToolCallCount);
        Assert.Equal(0, state.Budget.Usage.ModelTokenCount);
        Assert.Equal(0m, state.Budget.Usage.EstimatedCost);
        Assert.Equal(0, state.Budget.Usage.ContextSize);
        Assert.Equal(0, state.Budget.Usage.RepeatedToolCallCount);
        Assert.Null(state.ContinuationContext);
    }

    [Theory, AutoDomainData]
    public void Given_ACollectionOfAvailableTools_When_Start_Then_CopiesTheCollection(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var tools = new List<AiToolDefinition>
        {
            new(
                AiToolNames.SearchInternalData,
                "Search internal data.",
                JsonSerializer.SerializeToElement(new { type = "object" }))
        };
        var conversationHistory = new List<AiConversationMessage>
        {
            new(AiConversationRole.User, "Previous question")
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
        var state = MessageOrchestrationState.Start(
            processing,
            selectedModel,
            conversationHistory,
            tools,
            limits,
            startedAtUtc);

        // When
        tools.Clear();
        conversationHistory.Clear();

        // Then
        Assert.Single(state.AllowedTools);
        Assert.Single(state.ConversationHistory);
    }

    [Theory, AutoDomainData]
    public void Given_AResponseContinuation_When_RecordModelResponse_Then_KeepsTheOpaqueContext(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset startedAtUtc,
        string continuationToken)
    {
        // Given
        var limits = new OrchestrationExecutionLimits(
            MaximumExecutionTime: TimeSpan.FromMinutes(2),
            MaximumToolCalls: 8,
            MaximumModelTokens: 12_000,
            MaximumEstimatedCost: 1.25m,
            RetrievalCandidateLimit: 20,
            FinalEvidenceLimit: 8,
            MaximumContextSize: 30_000,
            MaximumRepeatedToolCalls: 2);
        var state = MessageOrchestrationState.Start(
            processing,
            selectedModel,
            [],
            [],
            limits,
            startedAtUtc);
        var continuation = new AiModelContinuationContext(
            selectedModel.Provider,
            continuationToken);
        var response = new AiModelResponse(
            new AiModelDecision(
                AiModelDecisionType.UseTools,
                "Tools required.",
                [],
                Answer: null,
                CitedEvidenceIds: []),
            new AiModelUsage(10, 5, 1, 0, 0.01m),
            continuation);

        // When
        state.RecordModelResponse(response, startedAtUtc);

        // Then
        Assert.Same(continuation, state.ContinuationContext);
    }

    [Theory, AutoDomainData]
    public void Given_ThreeAllowedReformulations_When_TryRequireGroundednessReformulation_Then_StopsAfterThreeAttempts(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var state = MessageOrchestrationState.Start(
            processing,
            selectedModel,
            [],
            [],
            new OrchestrationExecutionLimits(
                TimeSpan.FromMinutes(2),
                8,
                12_000,
                1.25m,
                20,
                8,
                30_000,
                2),
            startedAtUtc);

        // When
        var attempts = Enumerable.Range(0, 4)
            .Select(_ => state.TryRequireGroundednessReformulation(3))
            .ToArray();

        // Then
        Assert.Equal([true, true, true, false], attempts);
        Assert.True(state.GroundednessReformulationRequired);
        Assert.Equal(3, state.GroundednessReformulationCount);
    }

    [Theory, AutoDomainData]
    public void Given_TwentyRetrievedCandidates_When_RecordToolResults_Then_ExposesOnlyTheBestEightEvidenceItems(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var limits = new OrchestrationExecutionLimits(
            MaximumExecutionTime: TimeSpan.FromMinutes(2),
            MaximumToolCalls: 8,
            MaximumModelTokens: 12_000,
            MaximumEstimatedCost: 1.25m,
            RetrievalCandidateLimit: 20,
            FinalEvidenceLimit: 8,
            MaximumContextSize: 30_000,
            MaximumRepeatedToolCalls: 2);
        var state = MessageOrchestrationState.Start(
            processing,
            selectedModel,
            [],
            [],
            limits,
            startedAtUtc);
        var evidence = Enumerable.Range(1, 20)
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

        // When
        state.RecordToolResults([ToolExecutionResult.Succeeded("tool-call", evidence)]);

        // Then
        Assert.Equal(8, state.CollectedEvidence.Count);
        Assert.Equal(
            Enumerable.Range(13, 8).Reverse().Select(value => (double?)value),
            state.CollectedEvidence.Select(item => item.RelevanceScore));
        Assert.Equal(
            Enumerable.Range(13, 8).Reverse().Select(value => $"evidence-{value}"),
            state.CollectedEvidence.Select(item => item.EvidenceId));
    }

    [Theory, AutoDomainData]
    public void Given_MultipleRetrievalsWithDifferentScoreScales_When_RecordToolResults_Then_RetainsEvidenceFromEachRetrieval(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var limits = new OrchestrationExecutionLimits(
            MaximumExecutionTime: TimeSpan.FromMinutes(2),
            MaximumToolCalls: 8,
            MaximumModelTokens: 12_000,
            MaximumEstimatedCost: 1.25m,
            RetrievalCandidateLimit: 20,
            FinalEvidenceLimit: 3,
            MaximumContextSize: 30_000,
            MaximumRepeatedToolCalls: 2);
        var state = MessageOrchestrationState.Start(
            processing,
            selectedModel,
            [],
            [],
            limits,
            startedAtUtc);
        var dominantEvidence = Enumerable.Range(1, 3)
            .Select(index => new RetrievedEvidence(
                $"metalpro-{index}",
                "Microsoft365",
                "MetalPro",
                $"MetalPro content {index}",
                $"metalpro-reference-{index}",
                null,
                null,
                10 - index))
            .ToArray();
        var nordikEvidence = new RetrievedEvidence(
            "atelier-nordik",
            "Microsoft365",
            "Atelier Nordik",
            "Atelier Nordik accounting content",
            "atelier-nordik-reference",
            null,
            null,
            0.03);
        var financialEvidence = new RetrievedEvidence(
            "financial-calculations",
            "Microsoft365",
            "Financial calculations",
            "Break-even calculations",
            "financial-calculations-reference",
            null,
            null,
            0.02);

        // When
        state.RecordToolResults(
        [
            ToolExecutionResult.Succeeded("metalpro-call", dominantEvidence),
            ToolExecutionResult.Succeeded("nordik-call", [nordikEvidence]),
            ToolExecutionResult.Succeeded("financial-call", [financialEvidence])
        ]);

        // Then
        Assert.Equal(
            ["metalpro-1", "atelier-nordik", "financial-calculations"],
            state.CollectedEvidence.Select(evidence => evidence.EvidenceId));
    }

    [Theory, AutoDomainData]
    public void Given_MoreEvidenceThanTheFinalLimit_When_ModelVisibleToolResults_Then_ExposesOnlyRetainedEvidence(
        StartedMessageProcessing processing,
        SelectedAiModel selectedModel,
        DateTimeOffset startedAtUtc)
    {
        // Given
        var limits = new OrchestrationExecutionLimits(
            MaximumExecutionTime: TimeSpan.FromMinutes(2),
            MaximumToolCalls: 8,
            MaximumModelTokens: 12_000,
            MaximumEstimatedCost: 1.25m,
            RetrievalCandidateLimit: 20,
            FinalEvidenceLimit: 2,
            MaximumContextSize: 30_000,
            MaximumRepeatedToolCalls: 2);
        var state = MessageOrchestrationState.Start(
            processing,
            selectedModel,
            [],
            [],
            limits,
            startedAtUtc);
        var evidence = Enumerable.Range(1, 3)
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

        // When
        state.RecordToolResults([ToolExecutionResult.Succeeded("tool-call", evidence)]);

        // Then
        var result = Assert.Single(state.ModelVisibleToolResults);
        Assert.Equal(["evidence-3", "evidence-2"], result.Evidence.Select(item => item.EvidenceId));
        Assert.Equal(["evidence-1", "evidence-2", "evidence-3"], state.ToolResults.Single().Evidence.Select(item => item.EvidenceId));
    }
}
