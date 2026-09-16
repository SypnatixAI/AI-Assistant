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
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

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
            .SelectMany(connector => CreateToolDefinitions(connector, executableTools))
            .ToArray();

        if (tools.Length > 0)
        {
            memoryCache?.Set(cacheKey, tools, CacheDuration);
        }
        return tools;
    }

    public void InvalidateCache(Guid organizationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        memoryCache?.Remove($"ai-tools:{organizationId:D}");
    }

    private static IReadOnlyCollection<AiToolDefinition> CreateToolDefinitions(
        OrganizationConnector connector,
        IReadOnlySet<string> executableTools)
    {
        if (connector.Type != ConnectorType.Microsoft365
            || !executableTools.Contains(AiToolNames.AnalyzeMicrosoft365Spreadsheet)
            || !connector.Sources.Any(source => source.SourceType is
                Microsoft365SourceType.SharePoint or Microsoft365SourceType.OneDrive))
        {
            return [];
        }

        return [CreateMicrosoft365SpreadsheetAnalysisTool()];
    }

    private static AiToolDefinition CreateMicrosoft365SpreadsheetAnalysisTool() =>
        new(
            AiToolNames.AnalyzeMicrosoft365Spreadsheet,
            "Analyser exhaustivement un classeur Excel Microsoft 365 autorise avec des calculs et filtres deterministes.",
            CreateObjectSchema(
                new Dictionary<string, object>
                {
                    ["fileName"] = StringProperty("Nom exact du fichier XLSX ou XLSM, avec son extension."),
                    ["worksheetName"] = NullableProperty(new
                    {
                        type = "string",
                        description = "Nom de la feuille. Obligatoire lorsque le classeur contient plusieurs feuilles."
                    }),
                    ["aggregations"] = NullableProperty(new
                    {
                        type = "array",
                        description = "Calculs executes sur toutes les lignes avant les filtres, ou null si aucun calcul n'est requis.",
                        items = CreateObjectSchema(new Dictionary<string, object>
                        {
                            ["alias"] = StringProperty("Nom unique permettant de reutiliser le resultat dans un filtre."),
                            ["operation"] = EnumStringProperty(
                                "Operation mathematique.",
                                "average", "sum", "minimum", "maximum", "count"),
                            ["column"] = StringProperty("Nom exact de la colonne numerique.")
                        })
                    }),
                    ["filters"] = NullableProperty(new
                    {
                        type = "array",
                        description = "Filtres combines avec AND et appliques a toutes les lignes, ou null pour ne pas filtrer.",
                        items = CreateObjectSchema(new Dictionary<string, object>
                        {
                            ["column"] = StringProperty("Nom exact de la colonne a filtrer."),
                            ["operator"] = EnumStringProperty(
                                "Operateur de comparaison.",
                                "equals", "not_equals", "greater_than", "greater_than_or_equal",
                                "less_than", "less_than_or_equal", "contains"),
                            ["value"] = NullableScalarProperty(
                                "Valeur litterale. Null si aggregationAlias est utilise."),
                            ["aggregationAlias"] = NullableProperty(new
                            {
                                type = "string",
                                description = "Alias d'un calcul a comparer. Null si value est utilise."
                            })
                        })
                    }),
                    ["selectColumns"] = NullableProperty(new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "Colonnes a retourner, ou null pour retourner toutes les colonnes."
                    })
                }));

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

    private static object EnumStringProperty(string description, params string[] values) => new
    {
        type = "string",
        @enum = values,
        description
    };

    private static object NullableScalarProperty(string description) => new
    {
        anyOf = new object[]
        {
            new { type = "string", description },
            new { type = "number", description },
            new { type = "boolean", description },
            new { type = "null" }
        }
    };

    private static object NullableProperty(object property) => new
    {
        anyOf = new object[]
        {
            property,
            new { type = "null" }
        }
    };
}
