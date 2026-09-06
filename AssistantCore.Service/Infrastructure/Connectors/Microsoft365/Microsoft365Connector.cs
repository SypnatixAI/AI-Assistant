using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Evidence;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365Connector(
    IMicrosoft365UserGroupResolver groupResolver,
    IMicrosoft365SharePointGroupResolver sharePointGroupResolver,
    IMicrosoft365SearchRepository searchRepository,
    IMicrosoft365SearchAccessVerifier accessVerifier,
    Microsoft365ConnectorOptions options,
    IEvidenceNormalizer evidenceNormalizer,
    IMicrosoft365QueryExpansionService? queryExpansionService = null,
    IMicrosoft365SearchResultFusionService? searchResultFusionService = null,
    ILogger<Microsoft365Connector>? logger = null) : IMicrosoft365Connector
{
    public async Task<ConnectorResult> SearchAsync(
        SearchMicrosoft365ToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (context.OrganizationId == Guid.Empty
            || context.MemberId == Guid.Empty
            || context.IdentityProvider != IdentityProvider.MicrosoftEntraId
            || string.IsNullOrWhiteSpace(context.ExternalTenantId)
            || context.EntraUserId is null
            || context.EntraUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(context.UserEmail))
        {
            throw new InvalidOperationException(
                "The authenticated member cannot be resolved to a Microsoft Entra identity.");
        }

        var normalizedUserId = context.EntraUserId.Value.ToString("D");
        var groupIds = await groupResolver.ResolveGroupIdsAsync(
            context.ExternalTenantId!,
            normalizedUserId,
            cancellationToken);
        var sharePointGroupIds = await sharePointGroupResolver.ResolveGroupIdsAsync(
            context.OrganizationId,
            context.ExternalTenantId!,
            context.UserEmail!,
            cancellationToken);
        var searchParameters = new Microsoft365SearchParameters(
            request.Query,
            request.SourceTypes,
            request.DateFrom,
            request.DateTo,
            new Microsoft365SearchSecurityContext(
                context.OrganizationId,
                normalizedUserId,
                groupIds,
                sharePointGroupIds),
            Math.Min(options.MaximumResults, context.RetrievalCandidateLimit));
        var records = await SearchAcrossQueriesAsync(searchParameters, cancellationToken);
        var authorizedRecords = await accessVerifier.KeepAuthorizedAsync(
            context.OrganizationId,
            context.ExternalTenantId!,
            normalizedUserId,
            groupIds,
            sharePointGroupIds,
            records,
            cancellationToken);
        var evidence = evidenceNormalizer.Normalize(
            authorizedRecords.Select(MapCandidate).ToArray(),
            new EvidenceNormalizationOptions(
                options.MaximumContentLength,
                context.RetrievalCandidateLimit));

        return new ConnectorResult(evidence);
    }

    private async Task<IReadOnlyCollection<Microsoft365SearchRecord>> SearchAcrossQueriesAsync(
        Microsoft365SearchParameters searchParameters,
        CancellationToken cancellationToken)
    {
        var startedAt = TimeProvider.System.GetTimestamp();
        var queries = queryExpansionService is null
            ? [searchParameters.Query]
            : await queryExpansionService.ExpandAsync(searchParameters.Query, cancellationToken);

        if (queries.Count == 1)
        {
            var records = await searchRepository.SearchAsync(searchParameters, cancellationToken);
            LogRetrieval(1, records.Count, records.Count, startedAt);
            return records;
        }

        var searchTasks = queries
            .Select(query => SearchExpandedQueryAsync(
                searchParameters with { Query = query },
                cancellationToken))
            .ToArray();
        var searchResults = await Task.WhenAll(searchTasks);
        var successfulResultSets = searchResults
            .Where(result => result.Exception is null)
            .Select(result => result.Records)
            .ToArray();

        if (successfulResultSets.Length == 0)
        {
            throw new InvalidOperationException(
                "All Microsoft 365 expanded searches failed.",
                searchResults.First(result => result.Exception is not null).Exception);
        }

        var recordsBeforeDeduplication = successfulResultSets.Sum(resultSet => resultSet.Count);
        var fusedRecords = (searchResultFusionService ?? new Microsoft365SearchResultFusionService())
            .Fuse(successfulResultSets, searchParameters.MaximumResults);

        LogRetrieval(
            queries.Count,
            recordsBeforeDeduplication,
            fusedRecords.Count,
            startedAt);

        return fusedRecords;
    }

    private async Task<ExpandedSearchResult> SearchExpandedQueryAsync(
        Microsoft365SearchParameters searchParameters,
        CancellationToken cancellationToken)
    {
        try
        {
            return new ExpandedSearchResult(
                await searchRepository.SearchAsync(searchParameters, cancellationToken),
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger?.LogWarning(
                exception,
                "A Microsoft 365 expanded search failed. Other query variants can still be used.");

            return new ExpandedSearchResult([], exception);
        }
    }

    private void LogRetrieval(
        int searchesExecuted,
        int recordsBeforeDeduplication,
        int recordsAfterDeduplication,
        long startedAt)
    {
        logger?.LogInformation(
            "Microsoft365 retrieval completed with {SearchesExecuted} searches, {RecordsBeforeDeduplication} records before deduplication and {RecordsAfterDeduplication} records after deduplication in {ElapsedMilliseconds} ms.",
            searchesExecuted,
            recordsBeforeDeduplication,
            recordsAfterDeduplication,
            TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);
    }

    private static EvidenceCandidate MapCandidate(Microsoft365SearchRecord record) => new(
        record.SourceType,
        record.Title,
        record.Content,
        record.Reference,
        record.Url,
        record.ModifiedAt,
        record.RelevanceScore);

    private sealed record ExpandedSearchResult(
        IReadOnlyCollection<Microsoft365SearchRecord> Records,
        Exception? Exception);
}
