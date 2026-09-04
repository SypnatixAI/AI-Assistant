using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Evidence;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Evidence;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365Connector(
    IMicrosoft365UserGroupResolver groupResolver,
    IMicrosoft365SearchRepository searchRepository,
    IMicrosoft365SearchAccessVerifier accessVerifier,
    Microsoft365ConnectorOptions options,
    IEvidenceNormalizer evidenceNormalizer) : IMicrosoft365Connector
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
            || context.EntraUserId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The authenticated member cannot be resolved to a Microsoft Entra identity.");
        }

        var normalizedUserId = context.EntraUserId.Value.ToString("D");
        var groupIds = await groupResolver.ResolveGroupIdsAsync(
            context.ExternalTenantId!,
            normalizedUserId,
            cancellationToken);
        var searchParameters = new Microsoft365SearchParameters(
            request.Query,
            request.SourceTypes,
            request.DateFrom,
            request.DateTo,
            new Microsoft365SearchSecurityContext(
                context.OrganizationId,
                normalizedUserId,
                groupIds),
            Math.Min(options.MaximumResults, context.RetrievalCandidateLimit));
        var records = await searchRepository.SearchAsync(searchParameters, cancellationToken);
        var authorizedRecords = await accessVerifier.KeepAuthorizedAsync(
            context.OrganizationId,
            context.ExternalTenantId!,
            normalizedUserId,
            groupIds,
            records,
            cancellationToken);
        var evidence = evidenceNormalizer.Normalize(
            authorizedRecords.Select(MapCandidate).ToArray(),
            new EvidenceNormalizationOptions(
                options.MaximumContentLength,
                context.RetrievalCandidateLimit));

        return new ConnectorResult(evidence);
    }

    private static EvidenceCandidate MapCandidate(Microsoft365SearchRecord record) => new(
        record.SourceType,
        record.Title,
        record.Content,
        record.Reference,
        record.Url,
        record.ModifiedAt,
        record.RelevanceScore);
}
