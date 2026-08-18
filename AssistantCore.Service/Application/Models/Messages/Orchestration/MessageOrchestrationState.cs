using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed class MessageOrchestrationState
{
    private readonly List<RetrievedEvidence> _collectedEvidence = [];
    private readonly HashSet<string> _collectedEvidenceIds = new(StringComparer.Ordinal);
    private readonly List<string> _warnings = [];
    private readonly HashSet<string> _warningValues = new(StringComparer.Ordinal);
    private readonly List<AiRequestedToolCall> _requestedToolCalls = [];
    private readonly List<ToolExecutionResult> _toolResults = [];

    private MessageOrchestrationState(
        StartedMessageProcessing messageProcessing,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> allowedTools,
        OrchestrationExecutionLimits limits,
        DateTimeOffset startedAtUtc)
    {
        MessageProcessing = messageProcessing;
        SelectedModel = selectedModel;
        ConversationHistory = conversationHistory.ToArray();
        AllowedTools = allowedTools.ToArray();
        ToolExecutionContext = new ConnectorExecutionContext(
            messageProcessing.OrganizationId,
            messageProcessing.OwnerMemberId);
        Budget = new OrchestrationBudgetTracker(limits, startedAtUtc);
    }

    public StartedMessageProcessing MessageProcessing { get; }

    public string Question => MessageProcessing.UserMessage;

    public SelectedAiModel SelectedModel { get; }

    public IReadOnlyCollection<AiConversationMessage> ConversationHistory { get; }

    public IReadOnlyCollection<AiToolDefinition> AllowedTools { get; }

    public ConnectorExecutionContext ToolExecutionContext { get; }

    public OrchestrationBudgetTracker Budget { get; }

    public IReadOnlyCollection<RetrievedEvidence> CollectedEvidence =>
        _collectedEvidence.ToArray();

    public IReadOnlyCollection<string> Warnings => _warnings.ToArray();

    public IReadOnlyCollection<AiRequestedToolCall> RequestedToolCalls =>
        _requestedToolCalls.ToArray();

    public IReadOnlyCollection<ToolExecutionResult> ToolResults => _toolResults.ToArray();

    public AiModelContinuationContext? ContinuationContext { get; private set; }

    public bool HasExecutedToolCalls { get; private set; }

    public bool LastToolRoundAddedEvidence { get; private set; }

    public void RecordModelResponse(
        AiModelResponse response,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);

        ContinuationContext = response.ContinuationContext;
        Budget.RecordModelUsage(response.Usage, completedAtUtc);
    }

    public void AcceptToolCalls(
        IReadOnlyCollection<AiRequestedToolCall> toolCalls,
        IReadOnlyCollection<string> toolCallFingerprints,
        DateTimeOffset acceptedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(toolCalls);
        ArgumentNullException.ThrowIfNull(toolCallFingerprints);

        if (toolCalls.Count != toolCallFingerprints.Count)
        {
            throw new ArgumentException(
                "Every requested tool call must have one fingerprint.",
                nameof(toolCallFingerprints));
        }

        Budget.AcceptToolCalls(toolCallFingerprints, acceptedAtUtc);
        _requestedToolCalls.AddRange(toolCalls);
    }

    public void RecordToolResults(IReadOnlyCollection<ToolExecutionResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var evidenceCountBeforeToolRound = _collectedEvidence.Count;
        foreach (var result in results)
        {
            _toolResults.Add(result);
            CollectNewEvidence(result.Evidence);
            CollectNewWarnings(result.Warnings);
        }

        HasExecutedToolCalls = true;
        LastToolRoundAddedEvidence =
            _collectedEvidence.Count > evidenceCountBeforeToolRound;
    }

    private void CollectNewEvidence(IReadOnlyCollection<RetrievedEvidence> evidence)
    {
        foreach (var item in evidence)
        {
            if (_collectedEvidenceIds.Add(item.EvidenceId))
            {
                _collectedEvidence.Add(item);
            }
        }
    }

    private void CollectNewWarnings(IReadOnlyCollection<string> warnings)
    {
        foreach (var warning in warnings)
        {
            if (_warningValues.Add(warning))
            {
                _warnings.Add(warning);
            }
        }
    }

    public static MessageOrchestrationState Start(
        StartedMessageProcessing messageProcessing,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> allowedTools,
        OrchestrationExecutionLimits limits,
        DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(messageProcessing);
        ArgumentNullException.ThrowIfNull(selectedModel);
        ArgumentNullException.ThrowIfNull(conversationHistory);
        ArgumentNullException.ThrowIfNull(allowedTools);
        ArgumentNullException.ThrowIfNull(limits);

        return new MessageOrchestrationState(
            messageProcessing,
            selectedModel,
            conversationHistory,
            allowedTools,
            limits,
            startedAtUtc);
    }
}
