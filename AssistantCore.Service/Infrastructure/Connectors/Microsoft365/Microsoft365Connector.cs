using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Evidence;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Rag;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    ILogger<Microsoft365Connector>? logger = null,
    ICorrectiveRetrievalService? correctiveRetrieval = null,
    IAgenticRetrievalClient? agenticRetrievalClient = null,
    IOptions<AzureAiSearchOptions>? searchOptions = null) : IMicrosoft365Connector
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

        var entraGroupsStartedAt = TimeProvider.System.GetTimestamp();
        var groupIds = await groupResolver.ResolveGroupIdsAsync(
            context.ExternalTenantId!,
            normalizedUserId,
            cancellationToken);
        logger?.LogInformation(
            "Microsoft365 Entra group resolution completed in {ElapsedMilliseconds} ms with {GroupCount} groups.",
            TimeProvider.System.GetElapsedTime(entraGroupsStartedAt).TotalMilliseconds,
            groupIds.Count);

        var sharePointGroupsStartedAt = TimeProvider.System.GetTimestamp();
        var sharePointGroupIds = await sharePointGroupResolver.ResolveGroupIdsAsync(
            context.OrganizationId,
            context.ExternalTenantId!,
            context.UserEmail!,
            cancellationToken);
        logger?.LogInformation(
            "Microsoft365 SharePoint group resolution completed in {ElapsedMilliseconds} ms with {GroupCount} groups.",
            TimeProvider.System.GetElapsedTime(sharePointGroupsStartedAt).TotalMilliseconds,
            sharePointGroupIds.Count);
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
        if (options.AgenticRetrieval.Enabled && agenticRetrievalClient is not null)
        {
            return await SearchWithAgenticRetrievalAsync(
                request,
                context,
                normalizedUserId,
                groupIds,
                sharePointGroupIds,
                searchParameters,
                cancellationToken);
        }

        async Task<IReadOnlyCollection<Microsoft365SearchRecord>> SearchAuthorizedAsync(
            Microsoft365SearchParameters parameters, CancellationToken token)
        {
            var records = parameters.TextOnly
                ? await searchRepository.SearchAsync(parameters, token)
                : await SearchAcrossQueriesAsync(parameters, token);
            return await accessVerifier.KeepAuthorizedAsync(
                context.OrganizationId,
                context.ExternalTenantId!,
                normalizedUserId,
                groupIds,
                sharePointGroupIds,
                records,
                token);
        }
        var authorizedRecords = correctiveRetrieval is null
            ? await SearchAuthorizedAsync(searchParameters, cancellationToken)
            : await correctiveRetrieval.RetrieveAsync(searchParameters, context, SearchAuthorizedAsync, cancellationToken);
        var evidence = evidenceNormalizer.Normalize(
            authorizedRecords.Select(MapCandidate).ToArray(),
            new EvidenceNormalizationOptions(
                options.MaximumContentLength,
                context.RetrievalCandidateLimit));

        return new ConnectorResult(evidence);
    }

    private async Task<ConnectorResult> SearchWithAgenticRetrievalAsync(
        SearchMicrosoft365ToolArguments request,
        ConnectorExecutionContext context,
        string normalizedUserId,
        IReadOnlyCollection<string> groupIds,
        IReadOnlyCollection<string> sharePointGroupIds,
        Microsoft365SearchParameters searchParameters,
        CancellationToken cancellationToken)
    {
        var configuration = searchOptions?.Value
            ?? throw new InvalidOperationException(
                "AzureSearch options are required for Microsoft 365 agentic retrieval.");
        var filter = Microsoft365SearchRepositoryAdapter.BuildFilter(searchParameters);
        var retrievalStartedAt = TimeProvider.System.GetTimestamp();
        var result = await agenticRetrievalClient!.RetrieveAsync(
            new AgenticRetrievalRequest(
                request.Query,
                MapConversationHistory(context.ConversationHistory),
                configuration.KnowledgeBaseName,
                configuration.KnowledgeSourceName,
                filter,
                searchParameters.MaximumResults,
                options.AgenticRetrieval.MaxRuntimeInSeconds,
                options.AgenticRetrieval.MaxOutputSizeInTokens),
            cancellationToken);
        logger?.LogInformation(
            "Microsoft365 agentic retrieval stage completed in {ElapsedMilliseconds} ms with {ReferenceCount} references.",
            TimeProvider.System.GetElapsedTime(retrievalStartedAt).TotalMilliseconds,
            result.References.Count);

        var records = result.References.Select(reference => new Microsoft365SearchRecord(
            "Microsoft365",
            reference.Title,
            reference.Content,
            reference.DocumentKey,
            reference.SiteId,
            reference.DriveId,
            reference.DriveItemId,
            reference.Url,
            reference.ModifiedAt,
            reference.RelevanceScore,
            reference.RelevanceScore)).ToArray();
        var accessVerificationStartedAt = TimeProvider.System.GetTimestamp();
        var authorizedRecords = await accessVerifier.KeepAuthorizedAsync(
            context.OrganizationId,
            context.ExternalTenantId!,
            normalizedUserId,
            groupIds,
            sharePointGroupIds,
            records,
            cancellationToken);
        logger?.LogInformation(
            "Microsoft365 post-retrieval ACL stage completed in {ElapsedMilliseconds} ms with {AuthorizedCount} authorized records from {ReferenceCount} references.",
            TimeProvider.System.GetElapsedTime(accessVerificationStartedAt).TotalMilliseconds,
            authorizedRecords.Count,
            records.Length);

        var evidence = evidenceNormalizer.Normalize(
            authorizedRecords.Select(MapCandidate).ToArray(),
            new EvidenceNormalizationOptions(
                options.MaximumContentLength,
                context.RetrievalCandidateLimit));

        LogAgenticRetrieval(result.Activity, records.Length, authorizedRecords.Count);

        return new ConnectorResult(evidence);
    }

    private void LogAgenticRetrieval(
        IReadOnlyCollection<AgenticRetrievalActivity> activity,
        int recordsBeforeAccessVerification,
        int recordsAfterAccessVerification)
    {
        var subqueryCount = activity.Count(item =>
            string.Equals(item.Type, "searchIndex", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(item.Search));

        logger?.LogInformation(
            "Microsoft365 agentic retrieval completed with {SubqueryCount} observed subqueries, {RecordsBeforeAccessVerification} records before access verification and {RecordsAfterAccessVerification} records after access verification.",
            subqueryCount,
            recordsBeforeAccessVerification,
            recordsAfterAccessVerification);
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

    private static IReadOnlyCollection<AgenticRetrievalMessage> MapConversationHistory(
        IReadOnlyCollection<AiConversationMessage>? conversationHistory) =>
        conversationHistory?
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new AgenticRetrievalMessage(
                message.Role,
                message.Content))
            .ToArray()
        ?? [];

    private sealed record ExpandedSearchResult(
        IReadOnlyCollection<Microsoft365SearchRecord> Records,
        Exception? Exception);
}
