using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Evidence;

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
        ConnectorExecutionContext toolExecutionContext,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> allowedTools,
        OrchestrationExecutionLimits limits,
        DateTimeOffset startedAtUtc)
    {
        MessageProcessing = messageProcessing;
        SelectedModel = selectedModel;
        ConversationHistory = LimitConversationHistory(
            conversationHistory,
            limits.MaximumContextSize);
        AllowedTools = allowedTools.ToArray();
        ToolExecutionContext = toolExecutionContext with
        {
            RetrievalCandidateLimit = limits.RetrievalCandidateLimit
        };
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
        EvidenceNormalizer.Limit(
            _collectedEvidence,
            Budget.Limits.FinalEvidenceLimit);

    public IReadOnlyCollection<string> Warnings => _warnings.ToArray();

    public IReadOnlyCollection<AiRequestedToolCall> RequestedToolCalls =>
        _requestedToolCalls.ToArray();

    public IReadOnlyCollection<ToolExecutionResult> ToolResults => _toolResults.ToArray();

    public AiModelContinuationContext? ContinuationContext { get; private set; }

    public bool HasExecutedToolCalls { get; private set; }

    public bool LastToolRoundAddedEvidence { get; private set; }

    public bool FinalResponseRequired { get; private set; }

    public OrchestrationBudgetType? FinalResponseBudget { get; private set; }

    private static IReadOnlyCollection<AiConversationMessage> LimitConversationHistory(
        IReadOnlyCollection<AiConversationMessage> history,
        int maximumContextSize)
    {
        // Keep a conservative character budget for history so the current question,
        // tools and evidence still have room in the model context.
        var maximumHistoryCharacters = Math.Max(1, maximumContextSize * 3 / 4);
        var selected = new List<AiConversationMessage>();
        var characterCount = 0;

        foreach (var message in history.Reverse())
        {
            var messageCharacters = message.Content.Length;
            if (selected.Count > 0
                && characterCount + messageCharacters > maximumHistoryCharacters)
            {
                break;
            }

            selected.Add(message);
            characterCount += messageCharacters;
        }

        selected.Reverse();
        return selected;
    }

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

    public void RequireFinalResponse(OrchestrationBudgetType exceededBudget)
    {
        FinalResponseRequired = true;
        FinalResponseBudget = exceededBudget;
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
        ConnectorExecutionContext toolExecutionContext,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> allowedTools,
        OrchestrationExecutionLimits limits,
        DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(messageProcessing);
        ArgumentNullException.ThrowIfNull(toolExecutionContext);
        ArgumentNullException.ThrowIfNull(selectedModel);
        ArgumentNullException.ThrowIfNull(conversationHistory);
        ArgumentNullException.ThrowIfNull(allowedTools);
        ArgumentNullException.ThrowIfNull(limits);

        return new MessageOrchestrationState(
            messageProcessing,
            toolExecutionContext,
            selectedModel,
            conversationHistory,
            allowedTools,
            limits,
            startedAtUtc);
    }

    public static MessageOrchestrationState Start(
        StartedMessageProcessing messageProcessing,
        SelectedAiModel selectedModel,
        IReadOnlyCollection<AiConversationMessage> conversationHistory,
        IReadOnlyCollection<AiToolDefinition> allowedTools,
        OrchestrationExecutionLimits limits,
        DateTimeOffset startedAtUtc) =>
        Start(
            messageProcessing,
            new ConnectorExecutionContext(
                messageProcessing.OrganizationId,
                messageProcessing.OwnerMemberId),
            selectedModel,
            conversationHistory,
            allowedTools,
            limits,
            startedAtUtc);
}
