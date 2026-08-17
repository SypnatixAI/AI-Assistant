using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Models.Messages.Orchestration;

public sealed class OrchestrationBudgetTracker
{
    private readonly Dictionary<string, int> _acceptedToolCallCountsByFingerprint =
        new(StringComparer.Ordinal);

    public OrchestrationBudgetTracker(
        OrchestrationExecutionLimits limits,
        DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(limits);

        Limits = limits;
        StartedAtUtc = startedAtUtc;
        DeadlineUtc = startedAtUtc.Add(limits.MaximumExecutionTime);
    }

    public OrchestrationExecutionLimits Limits { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset DeadlineUtc { get; }

    public OrchestrationExecutionUsage Usage { get; private set; }
        = OrchestrationExecutionUsage.Empty;

    public void RecordModelUsage(AiModelUsage modelUsage, DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(modelUsage);

        Usage = Usage with
        {
            ExecutionTime = completedAtUtc - StartedAtUtc,
            InputTokens = Usage.InputTokens + modelUsage.InputTokens,
            OutputTokens = Usage.OutputTokens + modelUsage.OutputTokens,
            ModelCallCount = Usage.ModelCallCount + modelUsage.ModelCallCount,
            EstimatedCost = Usage.EstimatedCost + (modelUsage.EstimatedCost ?? 0m),
            ContextSize = modelUsage.InputTokens
        };
    }

    public OrchestrationBudgetDecision EvaluateToolCalls(
        int requestedToolCallCount,
        IReadOnlyCollection<string> toolCallFingerprints,
        DateTimeOffset currentTimeUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestedToolCallCount);
        ArgumentNullException.ThrowIfNull(toolCallFingerprints);

        if (currentTimeUtc >= DeadlineUtc)
        {
            return Exceeded(OrchestrationBudgetType.ExecutionTime);
        }

        if (Usage.ToolCallCount + requestedToolCallCount > Limits.MaximumToolCalls)
        {
            return Exceeded(OrchestrationBudgetType.ToolCalls);
        }

        if (Usage.ModelTokenCount >= Limits.MaximumModelTokens)
        {
            return Exceeded(OrchestrationBudgetType.ModelTokens);
        }

        if (Usage.EstimatedCost >= Limits.MaximumEstimatedCost)
        {
            return Exceeded(OrchestrationBudgetType.EstimatedCost);
        }

        if (Usage.ContextSize >= Limits.MaximumContextSize)
        {
            return Exceeded(OrchestrationBudgetType.ContextSize);
        }

        if (WouldExceedRepeatedToolCallLimit(toolCallFingerprints))
        {
            return Exceeded(OrchestrationBudgetType.RepeatedToolCalls);
        }

        return OrchestrationBudgetDecision.Allowed;
    }

    public void AcceptToolCalls(
        IReadOnlyCollection<string> toolCallFingerprints,
        DateTimeOffset acceptedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(toolCallFingerprints);

        var decision = EvaluateToolCalls(
            toolCallFingerprints.Count,
            toolCallFingerprints,
            acceptedAtUtc);
        if (!decision.IsAllowed)
        {
            throw new OrchestrationBudgetExceededException(
                decision.ExceededBudget!.Value);
        }

        var repeatedToolCallCount = CountRepeatedToolCalls(toolCallFingerprints);
        foreach (var fingerprint in toolCallFingerprints)
        {
            _acceptedToolCallCountsByFingerprint.TryGetValue(fingerprint, out var count);
            _acceptedToolCallCountsByFingerprint[fingerprint] = count + 1;
        }

        Usage = Usage with
        {
            ExecutionTime = acceptedAtUtc - StartedAtUtc,
            ToolCallCount = Usage.ToolCallCount + toolCallFingerprints.Count,
            RepeatedToolCallCount = Usage.RepeatedToolCallCount + repeatedToolCallCount
        };
    }

    private bool WouldExceedRepeatedToolCallLimit(
        IReadOnlyCollection<string> toolCallFingerprints)
    {
        var requestedCounts = toolCallFingerprints
            .GroupBy(fingerprint => fingerprint, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var (fingerprint, requestedCount) in requestedCounts)
        {
            _acceptedToolCallCountsByFingerprint.TryGetValue(fingerprint, out var acceptedCount);
            var repeatedCount = Math.Max(0, acceptedCount + requestedCount - 1);
            if (repeatedCount > Limits.MaximumRepeatedToolCalls)
            {
                return true;
            }
        }

        return false;
    }

    private int CountRepeatedToolCalls(IReadOnlyCollection<string> toolCallFingerprints)
    {
        var repeatedToolCallCount = 0;
        var currentBatchCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var fingerprint in toolCallFingerprints)
        {
            _acceptedToolCallCountsByFingerprint.TryGetValue(fingerprint, out var acceptedCount);
            currentBatchCounts.TryGetValue(fingerprint, out var currentBatchCount);
            if (acceptedCount + currentBatchCount > 0)
            {
                repeatedToolCallCount++;
            }

            currentBatchCounts[fingerprint] = currentBatchCount + 1;
        }

        return repeatedToolCallCount;
    }

    private static OrchestrationBudgetDecision Exceeded(
        OrchestrationBudgetType budget) => new(budget);
}
