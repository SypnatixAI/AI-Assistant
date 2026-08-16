using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiToolRegistryTests
{
    [Theory, AutoDomainData]
    public async Task Given_AllConnectorsAreAvailable_When_GetAvailableToolsAsync_Then_ReturnsStrictToolDefinitions(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var authenticateUserService = new StubAuthenticateUserService
        {
            Result = (organization, member)
        };
        var connectorQueries = new StubOrganizationConnectorQueries
        {
            Connectors =
            [
                CreateConnector(
                    ConnectorType.Microsoft365,
                    Microsoft365SourceType.SharePoint,
                    Microsoft365SourceType.OneDrive),
                CreateConnector(ConnectorType.Erp),
                CreateConnector(ConnectorType.Crm),
                CreateConnector(ConnectorType.InternalData)
            ]
        };
        var registry = new AiToolRegistry(
            authenticateUserService,
            connectorQueries,
            [new FakeErpConnector()],
            [new FakeCrmConnector()]);

        // When
        var tools = await registry.GetAvailableToolsAsync(CancellationToken.None);

        // Then
        Assert.Equal(
            [
                AiToolNames.SearchMicrosoft365,
                AiToolNames.QueryErp,
                AiToolNames.QueryCrm,
                AiToolNames.SearchInternalData
            ],
            tools.Select(tool => tool.Name));
        Assert.Equal(organization.Id, connectorQueries.ReceivedOrganizationId);

        foreach (var tool in tools)
        {
            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
            Assert.False(tool.InputSchema.GetProperty("additionalProperties").GetBoolean());

            var properties = tool.InputSchema.GetProperty("properties");
            var required = tool.InputSchema.GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray();
            Assert.Equal(properties.EnumerateObject().Select(property => property.Name), required);
        }

        var microsoft365Tool = tools.Single(
            tool => tool.Name == AiToolNames.SearchMicrosoft365);
        var allowedSourceTypes = microsoft365Tool.InputSchema
            .GetProperty("properties")
            .GetProperty("sourceTypes")
            .GetProperty("anyOf")[0]
            .GetProperty("items")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Equal(["onedrive", "sharepoint"], allowedSourceTypes);
    }

    [Theory, AutoDomainData]
    public async Task Given_Microsoft365HasNoAvailableSource_When_GetAvailableToolsAsync_Then_OmitsMicrosoft365Tool(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var authenticateUserService = new StubAuthenticateUserService
        {
            Result = (organization, member)
        };
        var connectorQueries = new StubOrganizationConnectorQueries
        {
            Connectors = [CreateConnector(ConnectorType.Microsoft365)]
        };
        var registry = new AiToolRegistry(
            authenticateUserService,
            connectorQueries,
            [],
            []);

        // When
        var tools = await registry.GetAvailableToolsAsync(CancellationToken.None);

        // Then
        Assert.Empty(tools);
    }

    [Theory, AutoDomainData]
    public async Task Given_ErpAndCrmHaveNoRegisteredAdapter_When_GetAvailableToolsAsync_Then_OmitsTheirTools(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var authenticateUserService = new StubAuthenticateUserService
        {
            Result = (organization, member)
        };
        var connectorQueries = new StubOrganizationConnectorQueries
        {
            Connectors =
            [
                CreateConnector(ConnectorType.Erp),
                CreateConnector(ConnectorType.Crm)
            ]
        };
        var registry = new AiToolRegistry(
            authenticateUserService,
            connectorQueries,
            [],
            []);

        // When
        var tools = await registry.GetAvailableToolsAsync(CancellationToken.None);

        // Then
        Assert.Empty(tools);
    }

    [Theory, AutoDomainData]
    public async Task Given_OnlyErpHasARegisteredAdapter_When_GetAvailableToolsAsync_Then_ReturnsOnlyErpTool(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var authenticateUserService = new StubAuthenticateUserService
        {
            Result = (organization, member)
        };
        var connectorQueries = new StubOrganizationConnectorQueries
        {
            Connectors =
            [
                CreateConnector(ConnectorType.Erp),
                CreateConnector(ConnectorType.Crm)
            ]
        };
        var registry = new AiToolRegistry(
            authenticateUserService,
            connectorQueries,
            [new FakeErpConnector()],
            []);

        // When
        var tools = await registry.GetAvailableToolsAsync(CancellationToken.None);

        // Then
        var tool = Assert.Single(tools);
        Assert.Equal(AiToolNames.QueryErp, tool.Name);
    }

    private static OrganizationConnector CreateConnector(
        ConnectorType type,
        params Microsoft365SourceType[] sourceTypes) => new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Status = RecordStatus.Active,
            IsConfigured = true,
            Sources = sourceTypes
                .Select(sourceType => new OrganizationConnectorSource
                {
                    SourceType = sourceType,
                    Status = RecordStatus.Active,
                    IsIndexed = true
                })
                .ToArray()
        };

    private sealed class StubOrganizationConnectorQueries : IOrganizationConnectorQueries
    {
        public IReadOnlyCollection<OrganizationConnector> Connectors { get; init; } = [];

        public Guid? ReceivedOrganizationId { get; private set; }

        public Task<IReadOnlyCollection<OrganizationConnector>> GetActiveConfiguredConnectors(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrganizationId = organizationId;
            return Task.FromResult(Connectors);
        }
    }
}
