using AssistantCore.ExternalServices.Services.Azure;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365SearchRepositoryAdapter(
    AzureAiSearchPassageSearchClient client,
    IOptions<AzureAiSearchOptions> options) : IMicrosoft365SearchRepository
{
    private static readonly IReadOnlySet<string> SupportedSourceTypes =
        new HashSet<string>(["sharepoint", "onedrive"], StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyCollection<Microsoft365SearchRecord>> SearchAsync(
        Microsoft365SearchParameters parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ValidateParameters(parameters);

        var configuration = options.Value;
        if (string.IsNullOrWhiteSpace(configuration.Endpoint)
            || string.IsNullOrWhiteSpace(configuration.IndexName))
        {
            throw new InvalidOperationException(
                "AzureSearch endpoint and index name are required for Microsoft 365 search.");
        }

        var filter = BuildSecurityFilter(parameters.SecurityContext);
        var results = await client.SearchAsync(
            configuration.Endpoint,
            configuration.IndexName,
            configuration.ApiKey,
            parameters.Query,
            filter,
            parameters.MaximumResults,
            cancellationToken);

        return results.Select(result => new Microsoft365SearchRecord(
                "Microsoft365",
                result.Title,
                result.Content,
                result.ChunkId,
                Url: null,
                ModifiedAt: null,
                result.Score))
            .ToArray();
    }

    internal static string BuildSecurityFilter(Microsoft365SearchSecurityContext securityContext)
    {
        ArgumentNullException.ThrowIfNull(securityContext);
        var organizationId = NormalizeIdentifier(
            securityContext.OrganizationId.ToString("D"),
            "organization");
        var userId = NormalizeIdentifier(securityContext.EntraUserId, "Microsoft Entra user");
        var groupIds = securityContext.EntraGroupIds
            .Select(groupId => NormalizeIdentifier(groupId, "Microsoft Entra group"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(groupId => groupId, StringComparer.Ordinal)
            .ToArray();

        var accessClauses = new List<string>
        {
            $"allowedUserIds/any(id: id eq '{userId}')"
        };
        if (groupIds.Length > 0)
        {
            accessClauses.Add(
                $"allowedGroupIds/any(id: search.in(id, '{string.Join(',', groupIds)}', ','))");
        }

        return $"organizationId eq '{organizationId}' and isAvailable eq true and ({string.Join(" or ", accessClauses)})";
    }

    private static void ValidateParameters(Microsoft365SearchParameters parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters.Query);
        ArgumentNullException.ThrowIfNull(parameters.SecurityContext);
        ArgumentNullException.ThrowIfNull(parameters.SecurityContext.EntraGroupIds);
        if (parameters.MaximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters));
        }

        if (parameters.DateFrom > parameters.DateTo)
        {
            throw new ArgumentException("The start date cannot be after the end date.", nameof(parameters));
        }

        if (parameters.SourceTypes?.Any(sourceType => !SupportedSourceTypes.Contains(sourceType)) == true)
        {
            throw new ArgumentException("An unsupported Microsoft 365 source type was requested.", nameof(parameters));
        }
    }

    private static string NormalizeIdentifier(string value, string name)
    {
        if (!Guid.TryParse(value, out var identifier) || identifier == Guid.Empty)
        {
            throw new ArgumentException($"A valid {name} identifier is required.");
        }

        return identifier.ToString("D");
    }
}
