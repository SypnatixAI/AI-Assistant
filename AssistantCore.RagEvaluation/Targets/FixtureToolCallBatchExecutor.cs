using AssistantCore.RagEvaluation.Models;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Orchestration;

namespace AssistantCore.RagEvaluation.Targets;

internal sealed class FixtureToolCallBatchExecutor(
    RagEvaluationCase evaluationCase,
    IReadOnlyDictionary<string, RetrievedEvidence> evidenceByReference,
    TimeProvider timeProvider) : IToolCallBatchExecutor
{
    private readonly ToolCallFingerprintGenerator _fingerprintGenerator = new();
    private readonly List<string> _retrievedReferences = [];
    private readonly List<string> _searchQueries = [];
    private int _roundIndex;

    public IReadOnlyCollection<string> RetrievedReferences => _retrievedReferences;

    public IReadOnlyCollection<string> SearchQueries => _searchQueries;

    public int ToolCallCount { get; private set; }

    public Task<IReadOnlyCollection<ToolExecutionResult>> ExecuteAsync(
        MessageOrchestrationState state,
        IReadOnlyCollection<AiRequestedToolCall> requestedToolCalls,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (requestedToolCalls.Any(call => !string.Equals(
                call.ToolName,
                AiToolNames.SearchInternalData,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The evaluation fixture supports only the internal search tool.");
        }

        var fingerprints = requestedToolCalls
            .Select(_fingerprintGenerator.CreateFingerprint)
            .ToArray();
        state.AcceptToolCalls(requestedToolCalls, fingerprints, timeProvider.GetUtcNow());

        var roundReferences = evaluationCase.Fixture.RetrievalRounds
            .ElementAtOrDefault(_roundIndex)
            ?? [];
        var evidence = roundReferences
            .Select(reference => evidenceByReference[reference])
            .ToArray();
        var results = requestedToolCalls
            .Select(toolCall => ToolExecutionResult.Succeeded(toolCall.CallId, evidence))
            .ToArray();

        foreach (var toolCall in requestedToolCalls)
        {
            if (toolCall.Arguments.TryGetProperty("query", out var query))
            {
                _searchQueries.Add(query.GetString() ?? string.Empty);
            }
        }

        _retrievedReferences.AddRange(roundReferences);
        ToolCallCount += requestedToolCalls.Count;
        _roundIndex++;
        state.RecordToolResults(results);

        return Task.FromResult<IReadOnlyCollection<ToolExecutionResult>>(results);
    }
}
