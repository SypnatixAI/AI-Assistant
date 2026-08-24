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
    IOrganizationMemberQueries memberQueries,
    IOrganizationQueries organizationQueries,
    IMicrosoft365UserGroupResolver groupResolver,
    IMicrosoft365SearchRepository searchRepository,
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

        var member = await memberQueries.FindMember(
            context.OrganizationId,
            context.MemberId,
            cancellationToken);
        if (member is null
            || member.Status != RecordStatus.Active
            || member.IdentityProvider != IdentityProvider.MicrosoftEntraId
            || !Guid.TryParse(member.ExternalUserId, out var entraUserId)
            || entraUserId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The authenticated member cannot be resolved to a Microsoft Entra identity.");
        }

        var organization = await organizationQueries.FindOrganization(
            context.OrganizationId,
            cancellationToken)
            ?? throw new InvalidOperationException("The authenticated organization was not found.");
        if (organization.IdentityProvider != IdentityProvider.MicrosoftEntraId)
        {
            throw new InvalidOperationException(
                "The authenticated organization is not connected to Microsoft Entra.");
        }

        var normalizedUserId = entraUserId.ToString("D");
        var groupIds = await groupResolver.ResolveGroupIdsAsync(
            organization,
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
            options.MaximumResults);
        var records = await searchRepository.SearchAsync(searchParameters, cancellationToken);
        var evidence = evidenceNormalizer.Normalize(
            records.Select(MapCandidate).ToArray(),
            new EvidenceNormalizationOptions(
                options.MaximumContentLength,
                options.MaximumResults));

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
