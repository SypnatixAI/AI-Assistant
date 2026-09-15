using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Queries;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiToolRegistryTests
{
    [Theory, AutoDomainData]
    public async Task Given_NoExecutionHandler_When_GetAvailableToolsAsync_Then_ReturnsNoTool(
        Organization organization)
    {
        // Given
        var connectorQueries = new StubOrganizationConnectorQueries
        {
            Connectors =
            [CreateConnector(ConnectorType.Microsoft365, Microsoft365SourceType.SharePoint)]
        };
        var registry = new AiToolRegistry(connectorQueries, []);

        // When
        var tools = await registry.GetAvailableToolsAsync(
            organization.Id,
            CancellationToken.None);

        // Then
        Assert.Empty(tools);
    }

    [Theory, AutoDomainData]
    public async Task Given_Microsoft365HasNoDocumentSource_When_GetAvailableToolsAsync_Then_ReturnsNoTool(
        Organization organization)
    {
        // Given
        var connectorQueries = new StubOrganizationConnectorQueries
        {
            Connectors = [CreateConnector(ConnectorType.Microsoft365)]
        };
        var registry = new AiToolRegistry(
            connectorQueries,
            [new FakeToolExecutionHandler(AiToolNames.AnalyzeMicrosoft365Spreadsheet)]);

        // When
        var tools = await registry.GetAvailableToolsAsync(
            organization.Id,
            CancellationToken.None);

        // Then
        Assert.Empty(tools);
    }

    [Theory, AutoDomainData]
    public async Task Given_Microsoft365SpreadsheetHandler_When_GetAvailableToolsAsync_Then_ReturnsAnalysisTool(
        Organization organization)
    {
        // Given
        var connectorQueries = new StubOrganizationConnectorQueries
        {
            Connectors = [CreateConnector(ConnectorType.Microsoft365, Microsoft365SourceType.SharePoint)]
        };
        var registry = new AiToolRegistry(
            connectorQueries,
            [new FakeToolExecutionHandler(AiToolNames.AnalyzeMicrosoft365Spreadsheet)]);

        // When
        var tools = await registry.GetAvailableToolsAsync(organization.Id, CancellationToken.None);

        // Then
        var tool = Assert.Single(tools);
        Assert.Equal(AiToolNames.AnalyzeMicrosoft365Spreadsheet, tool.Name);
        var properties = tool.InputSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("aggregations", out _));
        Assert.True(properties.TryGetProperty("filters", out _));
        Assert.Equal(organization.Id, connectorQueries.ReceivedOrganizationId);
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

    private sealed class FakeToolExecutionHandler(string toolName) : IAiToolExecutionHandler
    {
        public string ToolName { get; } = toolName;

        public Task<ToolExecutionResult> ExecuteAsync(
            ValidatedToolCall validatedToolCall,
            ConnectorExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ToolExecutionResult.Failed(
                validatedToolCall.CallId,
                ToolExecutionErrorCodes.ExecutorNotFound));
        }
    }
}
