using AssistantCore.Service.Application.Models.Messages.AgentRuntime;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.AgentRuntime;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class LegacyAgentRuntimeTests
{
    [Theory, AutoDomainData]
    public async Task Given_ARequest_When_RunAsync_Then_DelegatesToTheLegacyOrchestrator(
        StartedMessageProcessing processing,
        ConnectorExecutionContext executionContext,
        SelectedAiModel selectedModel,
        AiConversationMessage historyMessage,
        AiToolDefinition availableTool,
        MessageOrchestrationResult expectedResult)
    {
        // Given
        processing = processing with { ConversationHistory = [historyMessage] };
        var operations = new List<string>();
        var toolRegistry = new StubToolRegistry(operations, [availableTool]);
        var orchestrator = new StubMessageToolOrchestrator(operations, expectedResult);
        var runtime = new LegacyAgentRuntime(toolRegistry, orchestrator);

        // When
        var result = await runtime.RunAsync(
            new AgentTurnRequest(processing, executionContext, selectedModel),
            CancellationToken.None);

        // Then
        Assert.Same(expectedResult, result);
        Assert.Equal(["LoadTools", "Orchestrate"], operations);
        Assert.Equal(processing.OrganizationId, toolRegistry.ReceivedOrganizationId);
        Assert.Same(processing, orchestrator.ReceivedProcessing);
        Assert.Same(executionContext, orchestrator.ReceivedExecutionContext);
        Assert.Same(selectedModel, orchestrator.ReceivedSelectedModel);
        Assert.Equal([historyMessage], orchestrator.ReceivedConversationHistory);
        Assert.Equal([availableTool], orchestrator.ReceivedAvailableTools);
    }

    private sealed class StubToolRegistry(
        List<string> operations,
        IReadOnlyCollection<AiToolDefinition> availableTools) : IAiToolRegistry
    {
        public Guid? ReceivedOrganizationId { get; private set; }

        public Task<IReadOnlyCollection<AiToolDefinition>> GetAvailableToolsAsync(
            Guid organizationId,
            CancellationToken cancellationToken)
        {
            operations.Add("LoadTools");
            ReceivedOrganizationId = organizationId;

            return Task.FromResult(availableTools);
        }
    }

    private sealed class StubMessageToolOrchestrator(
        List<string> operations,
        MessageOrchestrationResult result) : IMessageToolOrchestrator
    {
        public StartedMessageProcessing? ReceivedProcessing { get; private set; }

        public ConnectorExecutionContext? ReceivedExecutionContext { get; private set; }

        public SelectedAiModel? ReceivedSelectedModel { get; private set; }

        public IReadOnlyCollection<AiConversationMessage> ReceivedConversationHistory { get; private set; } = [];

        public IReadOnlyCollection<AiToolDefinition> ReceivedAvailableTools { get; private set; } = [];

        public Task<MessageOrchestrationResult> OrchestrateAsync(
            StartedMessageProcessing processing,
            ConnectorExecutionContext executionContext,
            SelectedAiModel selectedModel,
            IReadOnlyCollection<AiConversationMessage> conversationHistory,
            IReadOnlyCollection<AiToolDefinition> availableTools,
            CancellationToken cancellationToken)
        {
            operations.Add("Orchestrate");
            ReceivedProcessing = processing;
            ReceivedExecutionContext = executionContext;
            ReceivedSelectedModel = selectedModel;
            ReceivedConversationHistory = conversationHistory;
            ReceivedAvailableTools = availableTools;

            return Task.FromResult(result);
        }

        public Task<MessageOrchestrationResult> OrchestrateStreamingAsync(
            StartedMessageProcessing processing,
            ConnectorExecutionContext executionContext,
            SelectedAiModel selectedModel,
            IReadOnlyCollection<AiConversationMessage> conversationHistory,
            IReadOnlyCollection<AiToolDefinition> availableTools,
            Func<string, CancellationToken, ValueTask> onProgress,
            Func<string, CancellationToken, ValueTask> onAnswerDelta,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
