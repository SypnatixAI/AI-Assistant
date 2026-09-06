using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public sealed class Microsoft365QueryExpansionService(
    IMicrosoft365QueryExpansionClient client,
    IMicrosoft365QueryExpansionBypassPolicy bypassPolicy,
    Microsoft365QueryExpansionOptions options,
    ILogger<Microsoft365QueryExpansionService> logger) : IMicrosoft365QueryExpansionService
{
    public async Task<IReadOnlyCollection<string>> ExpandAsync(
        string query,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (!options.Enabled || options.MaximumGeneratedQueries <= 0)
        {
            logger.LogInformation("Microsoft365 query expansion bypassed because it is disabled.");
            return [query.Trim()];
        }

        if (!bypassPolicy.ShouldExpand(query))
        {
            logger.LogInformation("Microsoft365 query expansion bypassed because the query is already concise.");
            return [query.Trim()];
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds));
        var startedAt = TimeProvider.System.GetTimestamp();

        try
        {
            var generatedQueries = await client.GenerateAsync(
                query,
                options.MaximumGeneratedQueries,
                timeoutSource.Token);
            var expandedQueries = NormalizeQueries(query, generatedQueries, options.MaximumGeneratedQueries);

            logger.LogInformation(
                "Microsoft365 query expansion generated {GeneratedQueryCount} variants in {ElapsedMilliseconds} ms.",
                Math.Max(0, expandedQueries.Count - 1),
                TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);

            return expandedQueries;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is OperationCanceledException
            or InvalidOperationException
            or FormatException
            or System.Text.Json.JsonException
            or ArgumentException)
        {
            logger.LogWarning(
                exception,
                "Microsoft365 query expansion failed after {ElapsedMilliseconds} ms. Falling back to the original query.",
                TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);

            return [query.Trim()];
        }
    }

    internal static IReadOnlyCollection<string> NormalizeQueries(
        string originalQuery,
        IReadOnlyCollection<string> generatedQueries,
        int maximumGeneratedQueries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalQuery);
        ArgumentNullException.ThrowIfNull(generatedQueries);

        var original = originalQuery.Trim();
        var queries = new List<string> { original };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            original
        };

        foreach (var generatedQuery in generatedQueries)
        {
            if (queries.Count >= maximumGeneratedQueries + 1)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(generatedQuery))
            {
                continue;
            }

            var normalizedQuery = generatedQuery.Trim();
            if (seen.Add(normalizedQuery))
            {
                queries.Add(normalizedQuery);
            }
        }

        return queries;
    }
}
