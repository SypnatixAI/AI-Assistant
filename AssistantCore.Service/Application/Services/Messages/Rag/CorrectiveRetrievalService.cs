using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Rag;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public sealed class CorrectiveRetrievalService(
    IRetrievalQualityEvaluator evaluator,
    AdaptiveRagReranker reranker,
    IRagPassageDiversifier diversifier,
    IOptions<RagOptions> options,
    TimeProvider timeProvider,
    ILogger<CorrectiveRetrievalService> logger) : ICorrectiveRetrievalService
{
    public async Task<IReadOnlyCollection<Microsoft365SearchRecord>> RetrieveAsync(
        Microsoft365SearchParameters parameters, ConnectorExecutionContext context,
        Func<Microsoft365SearchParameters, CancellationToken, Task<IReadOnlyCollection<Microsoft365SearchRecord>>> authorizedSearch,
        CancellationToken cancellationToken)
    {
        using var activity = RagTelemetry.Activities.StartActivity("rag.corrective_retrieval");
        activity?.SetTag("rag.vector.metric", options.Value.VectorSearch.Metric);
        var records = await authorizedSearch(parameters, cancellationToken);
        var settings = options.Value.CorrectiveRag;
        if (!settings.Enabled && !options.Value.Reranking.DedicatedCrossEncoderEnabled) return records;
        var started = timeProvider.GetTimestamp();
        var attempts = 0;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = context.Budget is null ? TimeSpan.Zero : context.Budget.DeadlineUtc - timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return records;
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(remaining.TotalSeconds, settings.MaximumStageDurationSeconds)));
        try
        {
            var quality = await EvaluateAsync(parameters.Query, records, context, timeout.Token);
            var previousLimit = parameters.MaximumResults;
            while (settings.Enabled && !quality.IsSufficient && attempts < settings.MaximumCorrectionAttempts)
            {
                timeout.Token.ThrowIfCancellationRequested();
                // First correction removes vector constraints; later attempts increase candidates.
                var limit = (int)Math.Min(Math.Min(100L, context.RetrievalCandidateLimit), (long)previousLimit * 2);
                if (attempts > 0 && limit <= previousLimit) break;
                if (context.Budget?.TryReserveAuxiliaryOperation(settings.EstimatedSearchCost, timeProvider.GetUtcNow()) != true) break;
                attempts++;
                context.RagStatus?.RecordCorrection();
                RagTelemetry.Record("rag.corrective.estimated_cost", (double)settings.EstimatedSearchCost);
                var corrected = await authorizedSearch(parameters with { TextOnly = true, MaximumResults = limit }, timeout.Token);
                var correctedQuality = await EvaluateAsync(parameters.Query, corrected, context, timeout.Token);
                if (correctedQuality.Confidence >= quality.Confidence)
                {
                    records = corrected;
                    quality = correctedQuality;
                }
                previousLimit = limit;
            }
            var passages = records.Select(ToPassage).ToArray();
            var ranked = await reranker.RerankAsync(parameters.Query, passages, quality, context, timeout.Token);
            // La diversification vient apres le classement : elle reduit la redondance
            // documentaire sans jamais introduire de preuve absente du classement.
            ranked = diversifier.Diversify(ranked);
            var byReference = records.ToDictionary(r => r.Reference, StringComparer.Ordinal);
            // Carry the selected ordering through the existing evidence normalizer.
            return ranked.Select((p, index) => byReference[p.Reference] with { RelevanceScore = ranked.Count - index }).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // Do not log provider exception messages, which can contain source content.
            logger.LogWarning("Corrective retrieval unavailable ({FailureType}); retaining authorized retrieval.", exception.GetType().Name);
            activity?.SetTag("rag.corrective.fallback", true);
            return records;
        }
        finally
        {
            RagTelemetry.Record("rag.corrective.triggered", attempts > 0 ? 1 : 0);
            RagTelemetry.Record("rag.corrective.attempt_count", attempts);
            RagTelemetry.Record("rag.retrieval.duration_ms", timeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async Task<RetrievalQualityResult> EvaluateAsync(string query, IReadOnlyCollection<Microsoft365SearchRecord> records,
        ConnectorExecutionContext context, CancellationToken cancellationToken)
    {
        var result = await evaluator.EvaluateAsync(query, records.Select(ToPassage).ToArray(), cancellationToken).WaitAsync(cancellationToken);
        if (!double.IsFinite(result.Confidence) || result.Confidence is < 0 or > 1)
            throw new InvalidOperationException("Invalid retrieval confidence.");
        context.RagStatus?.RecordQuality(result);
        RagTelemetry.Record("rag.retrieval.quality_score", result.Confidence);
        RagTelemetry.Record("rag.retrieval.sufficient", result.IsSufficient ? 1 : 0);
        return result;
    }

    private static RagPassage ToPassage(Microsoft365SearchRecord record) => new(
        record.Reference, record.Content, record.DriveItemId is null ? record.Url ?? record.Reference : $"{record.DriveId}/{record.DriveItemId}", record.SemanticScore);
}
