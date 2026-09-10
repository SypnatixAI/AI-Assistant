using System.Text.Json;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Models.Messages.Tools;
using Microsoft.Extensions.Caching.Memory;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public sealed class AiToolRegistry(
    IOrganizationConnectorQueries organizationConnectorQueries,
    IEnumerable<IAiToolExecutionHandler> toolHandlers,
    IMemoryCache? memoryCache = null) : IAiToolRegistry
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyCollection<AiToolDefinition>> GetAvailableToolsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);

        var cacheKey = $"ai-tools:{organizationId:D}";
        if (memoryCache is not null
            && memoryCache.TryGetValue(cacheKey, out IReadOnlyCollection<AiToolDefinition>? cachedTools)
            && cachedTools is not null)
        {
            return cachedTools;
        }

        var connectors = await organizationConnectorQueries.GetActiveConfiguredConnectors(
            organizationId,
            cancellationToken);
        var executableTools = toolHandlers
            .Select(handler => handler.ToolName)
            .ToHashSet(StringComparer.Ordinal);
        var tools = connectors
            .Select(connector => CreateToolDefinition(connector, executableTools))
            .OfType<AiToolDefinition>()
            .ToArray();

        memoryCache?.Set(cacheKey, tools, CacheDuration);
        return tools;
    }

    private AiToolDefinition? CreateToolDefinition(
        OrganizationConnector connector,
        IReadOnlySet<string> executableTools) =>
        connector.Type switch
        {
            ConnectorType.Microsoft365
                when executableTools.Contains(AiToolNames.SearchMicrosoft365) =>
                CreateMicrosoft365Tool(connector),
            _ => null
        };

    private static AiToolDefinition? CreateMicrosoft365Tool(OrganizationConnector connector)
    {
        var allowedSourceTypes = connector.Sources
            .Select(source => source.SourceType switch
            {
                Microsoft365SourceType.SharePoint => "sharepoint",
                Microsoft365SourceType.OneDrive => "onedrive",
                _ => null
            })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(sourceType => sourceType, StringComparer.Ordinal)
            .ToArray();

        if (allowedSourceTypes.Length == 0)
        {
            return null;
        }

        return new AiToolDefinition(
            AiToolNames.SearchMicrosoft365,
            "Rechercher dans les contenus Microsoft 365 autorises et deja indexes.",
            CreateObjectSchema(
                new Dictionary<string, object>
                {
                    ["query"] = StringProperty(
                        "Termes a rechercher dans les contenus Microsoft 365."),
                    ["sourceTypes"] = NullableProperty(new
                    {
                        type = "array",
                        items = new { type = "string", @enum = allowedSourceTypes },
                        description = "Sources a limiter, ou null pour toutes les sources autorisees."
                    }),
                    ["dateFrom"] = NullableDateProperty(
                        "Date minimale de modification des fichiers. Ne filtre pas les dates mentionnees dans leur contenu."),
                    ["dateTo"] = NullableDateProperty(
                        "Date maximale de modification des fichiers. Ne filtre pas les dates mentionnees dans leur contenu.")
                }));
    }

    private static JsonElement CreateObjectSchema(IReadOnlyDictionary<string, object> properties) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required = properties.Keys.ToArray(),
            additionalProperties = false
        });

    private static object StringProperty(string description) => new
    {
        type = "string",
        description
    };

    private static object NullableDateProperty(string description) =>
        NullableProperty(new
        {
            type = "string",
            description = $"{description} Format YYYY-MM-DD."
        });

    private static object NullableProperty(object property) => new
    {
        anyOf = new object[]
        {
            property,
            new { type = "null" }
        }
    };
}
