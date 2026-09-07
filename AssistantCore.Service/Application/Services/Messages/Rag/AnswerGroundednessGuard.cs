using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Rag;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public sealed class AnswerGroundednessGuard(
    IAnswerGroundednessEvaluator evaluator,
    IOptions<RagOptions> options,
    TimeProvider timeProvider,
    ILogger<AnswerGroundednessGuard> logger) : IAnswerGroundednessGuard
{
    public const string InsufficientEvidenceAnswer = "Les informations disponibles dans les sources accessibles ne permettent pas de confirmer une réponse à cette question.";

    public int MaximumReformulationAttempts =>
        options.Value.CorrectiveRag.MaximumGroundednessReformulationAttempts;

    public async Task<MessageOrchestrationResult> ValidateAsync(MessageOrchestrationState state, MessageOrchestrationResult result, CancellationToken cancellationToken)
    {
        var settings = options.Value.CorrectiveRag;
        if (!settings.Enabled || !settings.GroundednessCheckEnabled || !state.HasExecutedToolCalls) return result;
        // Internal-source answers are considered at risk, even with high retrieval scores.
        using var activity = RagTelemetry.Activities.StartActivity("rag.groundedness");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = state.Budget.DeadlineUtc - timeProvider.GetUtcNow();
        cancellationToken.ThrowIfCancellationRequested();
        if (remaining <= TimeSpan.Zero) return Degrade(result, "budget_exhausted");
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(remaining.TotalSeconds, settings.MaximumStageDurationSeconds)));
        try
        {
            var evidence = result.CitedEvidence
                .Select(p => new RagPassage(
                    p.Reference,
                    $"{p.Title}. {p.Content}",
                    p.Url ?? p.Reference))
                .ToArray();
            var check = await evaluator.EvaluateAsync(result.Answer, evidence, timeout.Token).WaitAsync(timeout.Token);
            var passed = check.Passed && double.IsFinite(check.Confidence) && check.Confidence >= settings.GroundednessConfidenceThreshold;
            RagTelemetry.Record("rag.groundedness.score", check.Confidence);
            RagTelemetry.Record("rag.groundedness.passed", passed ? 1 : 0);
            if (passed) return result;
            logger.LogWarning("Groundedness rejected answer: {Reason}; confidence {Confidence}; cited passages {EvidenceCount}.",
                check.Reason, check.Confidence, evidence.Length);
            return Degrade(
                result,
                evidence.Length == 0
                    ? "missing_evidence"
                    : "content_rejected");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return Degrade(result, "timeout");
        }
        catch (Exception exception)
        {
            logger.LogWarning("Groundedness validation unavailable ({FailureType}); returning an insufficient-evidence answer.", exception.GetType().Name);
            activity?.SetTag("rag.groundedness.fallback", true);
            return Degrade(result, "validator_error");
        }
    }

    private MessageOrchestrationResult Degrade(MessageOrchestrationResult result, string reason)
    {
        logger.LogWarning("Groundedness validation degraded: {Reason}.", reason);
        return result with
        {
            Answer = InsufficientEvidenceAnswer,
            CitedEvidence = [],
            Warnings = result.Warnings.Append("rag.groundedness.unverified")
                .Append($"rag.groundedness.{reason}").Distinct().ToArray()
        };
    }
}
