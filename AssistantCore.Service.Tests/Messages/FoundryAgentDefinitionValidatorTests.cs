using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Foundry;
using AssistantCore.ExternalServices.Services.Foundry;

namespace AssistantCore.Service.Tests.Messages;

public sealed class FoundryAgentDefinitionValidatorTests
{
    [Theory, AutoDomainData]
    public void Given_AllRequiredKnowledgeTools_When_Validate_Then_AcceptsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new { type = "function", name = "EnterpriseSearch" },
                new { type = "mcp", server_label = "work-iq" },
                new { type = "mcp", server_label = "foundry-iq" }
            }
        });

        // When
        var exception = Record.Exception(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Null(exception);
    }

    [Theory, AutoDomainData]
    public void Given_DirectWorkIqAndFoundryIqMcp_When_Validate_Then_AcceptsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new { type = "function", name = "EnterpriseSearch" },
                new { type = "work_iq_preview" },
                new { type = "mcp", server_label = "foundry-iq" }
            }
        });

        // When
        var exception = Record.Exception(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Null(exception);
    }

    [Theory, AutoDomainData]
    public void Given_CustomConfiguredMcpLabels_When_Validate_Then_AcceptsDefinition(
        string workIqServerLabel,
        string foundryIqServerLabel)
    {
        // Given
        var settings = new FoundryAgentClientSettings(
            "https://example.services.ai.azure.com/api/projects/project",
            "agent",
            "1",
            RequireEnterpriseSearch: false,
            RequireWorkIq: true,
            WorkIqServerLabel: workIqServerLabel,
            RequireFoundryIq: true,
            FoundryIqServerLabel: foundryIqServerLabel);
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new { type = "mcp", server_label = workIqServerLabel },
                new { type = "mcp", server_label = foundryIqServerLabel }
            }
        });

        // When
        var exception = Record.Exception(() =>
            FoundryAgentDefinitionValidator.Validate(definition, settings));

        // Then
        Assert.Null(exception);
    }

    [Theory, AutoDomainData]
    public void Given_MissingWorkIq_When_Validate_Then_RejectsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new { type = "function", name = "EnterpriseSearch" },
                new { type = "mcp", server_label = "foundry-iq" }
            }
        });

        // When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Contains("Work IQ", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public void Given_MissingFoundryIq_When_Validate_Then_RejectsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new { type = "function", name = "EnterpriseSearch" },
                new { type = "mcp", server_label = "work-iq" }
            }
        });

        // When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Contains("Foundry IQ", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public void Given_MissingEnterpriseSearch_When_Validate_Then_RejectsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new { type = "mcp", server_label = "work-iq" },
                new { type = "mcp", server_label = "foundry-iq" }
            }
        });

        // When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Contains("EnterpriseSearch", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public void Given_WebSearchAlongsideManagedKnowledge_When_Validate_Then_RejectsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new { type = "function", name = "EnterpriseSearch" },
                new { type = "mcp", server_label = "work-iq" },
                new { type = "mcp", server_label = "foundry-iq" },
                new { type = "web_search" }
            }
        });

        // When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Contains("web_search", exception.Message, StringComparison.Ordinal);
    }
}
