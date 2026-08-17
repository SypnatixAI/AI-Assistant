using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.AiModels;
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
            MaximumResultsPerTool: 20,
            MaximumContextSize: 30_000,
            MaximumRepeatedToolCalls: 2);

        // When
        var state = MessageOrchestrationState.Start(
            processing,
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
            MaximumResultsPerTool: 20,
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
            MaximumResultsPerTool: 20,
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
}
