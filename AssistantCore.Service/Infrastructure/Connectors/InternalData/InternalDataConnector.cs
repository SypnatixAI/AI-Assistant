using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Models.Messages.Evidence;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors;
using AssistantCore.Service.Application.Services.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Services.Messages.Evidence;

namespace AssistantCore.Service.Infrastructure.Connectors.InternalData;

public sealed class InternalDataConnector(
    IInternalDataSearchRepository repository,
    InternalDataConnectorOptions options,
    IEvidenceNormalizer evidenceNormalizer) : IInternalDataConnector
{
    private const int MaximumAllowedResults = 100;

    public async Task<ConnectorResult> SearchAsync(
        SearchInternalDataToolArguments request,
        ConnectorExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        ValidateOptions(options);

        var parameters = new InternalDataSearchParameters(
            context.OrganizationId,
            context.MemberId,
            request.Query,
            options.EnabledCategories,
            Math.Min(options.MaximumResults, context.RetrievalCandidateLimit));
        var records = await repository.SearchAsync(parameters, cancellationToken);
        var candidates = records.Select(MapCandidate).ToArray();
        var evidence = evidenceNormalizer.Normalize(
            candidates,
            new EvidenceNormalizationOptions(
                options.MaximumContentLength,
                Math.Min(options.MaximumResults, context.RetrievalCandidateLimit)));

        return new ConnectorResult(evidence);
    }

    private static EvidenceCandidate MapCandidate(InternalDataSearchRecord record)
    {
        var sourceType = record.Category switch
        {
            InternalDataCategory.Conversations => "InternalConversation",
            InternalDataCategory.Messages => "InternalMessage",
            _ => throw new ArgumentOutOfRangeException(
                nameof(record),
                record.Category,
                "Unsupported internal data category.")
        };
        var stableReference = $"{sourceType}:{record.Reference}";

        return new EvidenceCandidate(
            sourceType,
            record.Title,
            record.Content,
            stableReference,
            Url: null,
            OccurredAt: record.OccurredAt,
            RelevanceScore: record.RelevanceScore);
    }

    private static void ValidateOptions(InternalDataConnectorOptions configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.EnabledCategories);

        if (configuration.MaximumResults is <= 0 or > MaximumAllowedResults)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                $"The maximum number of results must be between 1 and {MaximumAllowedResults}.");
        }

        if (configuration.MaximumContentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "The maximum content length must be greater than zero.");
        }
    }
}
