using System.Text.Json;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using AssistantCore.Service.Application.Services.Messages.Connectors;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public sealed class AiToolRegistry(
    IAuthenticateUserService authenticateUserService,
    IOrganizationConnectorQueries organizationConnectorQueries,
    IEnumerable<IErpConnector> erpConnectors,
    IEnumerable<ICrmConnector> crmConnectors) : IAiToolRegistry
{
    public async Task<IReadOnlyCollection<AiToolDefinition>> GetAvailableToolsAsync(
        CancellationToken cancellationToken)
    {
        var (organization, _) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        var connectors = await organizationConnectorQueries.GetActiveConfiguredConnectors(
            organization.Id,
            cancellationToken);

        return connectors
            .Select(CreateToolDefinition)
            .OfType<AiToolDefinition>()
            .ToArray();
    }

    private AiToolDefinition? CreateToolDefinition(OrganizationConnector connector) =>
        connector.Type switch
        {
            ConnectorType.Microsoft365 => CreateMicrosoft365Tool(connector),
            ConnectorType.Erp when erpConnectors.Any() => CreateErpTool(),
            ConnectorType.Crm when crmConnectors.Any() => CreateCrmTool(),
            ConnectorType.InternalData => CreateInternalDataTool(),
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
                        uniqueItems = true,
                        description = "Sources a limiter, ou null pour toutes les sources autorisees."
                    }),
                    ["dateFrom"] = NullableDateProperty("Date minimale des contenus."),
                    ["dateTo"] = NullableDateProperty("Date maximale des contenus.")
                }));
    }

    private static AiToolDefinition CreateErpTool() => new(
        AiToolNames.QueryErp,
        "Lire les ventes, commandes, factures ou stocks dans l'ERP.",
        CreateObjectSchema(
            new Dictionary<string, object>
            {
                ["metric"] = new
                {
                    type = "string",
                    @enum = new[] { "sales", "orders", "invoices", "inventory" },
                    description = "Categorie de donnees ERP a lire."
                },
                ["dateFrom"] = NullableDateProperty("Date minimale de la periode."),
                ["dateTo"] = NullableDateProperty("Date maximale de la periode.")
            }));

    private static AiToolDefinition CreateCrmTool() => new(
        AiToolNames.QueryCrm,
        "Rechercher des clients, contacts ou opportunites dans le CRM.",
        CreateObjectSchema(
            new Dictionary<string, object>
            {
                ["query"] = StringProperty("Termes a rechercher dans le CRM."),
                ["entityTypes"] = NullableProperty(new
                {
                    type = "array",
                    items = new
                    {
                        type = "string",
                        @enum = new[] { "customers", "contacts", "opportunities" }
                    },
                    uniqueItems = true,
                    description = "Types d'entites a limiter, ou null pour tous les types autorises."
                })
            }));

    private static AiToolDefinition CreateInternalDataTool() => new(
        AiToolNames.SearchInternalData,
        "Rechercher dans les donnees internes autorisees.",
        CreateObjectSchema(
            new Dictionary<string, object>
            {
                ["query"] = StringProperty("Termes a rechercher dans les donnees internes.")
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
