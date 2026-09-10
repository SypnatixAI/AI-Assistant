using AssistantCore.Service.Application.Configuration;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Tabular;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Tabular;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365SpreadsheetDocumentResolver(
    IMicrosoft365UserGroupResolver groupResolver,
    IMicrosoft365SharePointGroupResolver sharePointGroupResolver,
    IMicrosoft365SearchAccessVerifier accessVerifier,
    IAgenticRetrievalClient retrievalClient,
    IOptions<AzureAiSearchOptions> searchOptions) : IMicrosoft365SpreadsheetDocumentResolver
{
    private const int MaximumDocumentCandidates = 20;

    public async Task<Microsoft365SpreadsheetDocument> ResolveAsync(
        string fileName,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(context);
        EnsureMicrosoftIdentity(context);

        var normalizedUserId = context.EntraUserId!.Value.ToString("D");
        var entraGroupsTask = groupResolver.ResolveGroupIdsAsync(
            context.ExternalTenantId!,
            normalizedUserId,
            cancellationToken);
        var sharePointGroupsTask = sharePointGroupResolver.ResolveGroupIdsAsync(
            context.OrganizationId,
            context.ExternalTenantId!,
            context.UserEmail!,
            cancellationToken);
        await Task.WhenAll(entraGroupsTask, sharePointGroupsTask);
        var entraGroups = await entraGroupsTask;
        var sharePointGroups = await sharePointGroupsTask;
        var securityContext = new Microsoft365SearchSecurityContext(
            context.OrganizationId,
            normalizedUserId,
            entraGroups,
            sharePointGroups);
        var filter = Microsoft365SearchFilterBuilder.Build(new Microsoft365SearchParameters(
            fileName,
            SourceTypes: null,
            DateFrom: null,
            DateTo: null,
            SecurityContext: securityContext,
            MaximumResults: MaximumDocumentCandidates));
        var configuration = searchOptions.Value;
        var retrieval = await retrievalClient.RetrieveAsync(
            new AgenticRetrievalRequest(
                $"Find the spreadsheet named '{fileName}'.",
                [],
                configuration.KnowledgeBaseName,
                configuration.KnowledgeSourceName,
                filter,
                MaximumDocumentCandidates,
                MaximumDocumentCandidates,
                configuration.KnowledgeBaseMaxRuntimeInSeconds,
                configuration.KnowledgeBaseMaxOutputSizeInTokens
                    ?? throw new InvalidOperationException(
                        "AzureSearch knowledge base output token limit is required.")),
            cancellationToken);

        var requestedTitle = Path.GetFileNameWithoutExtension(fileName);
        var matchingRecords = retrieval.References
            .Where(reference => string.Equals(
                reference.Title,
                requestedTitle,
                StringComparison.OrdinalIgnoreCase))
            .Select(reference => new Microsoft365SearchRecord(
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
                reference.RelevanceScore))
            .GroupBy(
                record => new { record.SiteId, record.DriveId, record.DriveItemId },
                record => record)
            .Select(group => group.First())
            .ToArray();
        var authorizedRecords = await accessVerifier.KeepAuthorizedAsync(
            context.OrganizationId,
            context.ExternalTenantId!,
            normalizedUserId,
            entraGroups,
            sharePointGroups,
            matchingRecords,
            cancellationToken);

        if (authorizedRecords.Count == 0)
        {
            throw new InvalidOperationException($"Spreadsheet '{fileName}' was not found or is not accessible.");
        }

        if (authorizedRecords.Count > 1)
        {
            throw new InvalidOperationException(
                $"Several accessible spreadsheets are named '{fileName}'; use a unique file name.");
        }

        var record = authorizedRecords.Single();
        return new Microsoft365SpreadsheetDocument(
            fileName,
            context.ExternalTenantId!,
            record.DriveId!,
            record.DriveItemId!,
            record.Reference,
            record.Url);
    }

    private static void EnsureMicrosoftIdentity(ConnectorExecutionContext context)
    {
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
    }
}
