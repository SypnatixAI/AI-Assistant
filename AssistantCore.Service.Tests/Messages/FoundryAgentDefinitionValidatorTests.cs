using System.Text.Json;
using AssistantCore.ExternalServices.Services.Foundry;

namespace AssistantCore.Service.Tests.Messages;

public sealed class FoundryAgentDefinitionValidatorTests
{
    [Theory, AutoDomainData]
    public void Given_EnterpriseSearchWithoutWebSearch_When_Validate_Then_AcceptsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new[]
            {
                new { type = "function", name = "EnterpriseSearch" }
            }
        });

        // When
        var exception = Record.Exception(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Null(exception);
    }

    [Theory, AutoDomainData]
    public void Given_NestedEnterpriseSearchDefinition_When_Validate_Then_AcceptsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new object[]
            {
                new
                {
                    type = "function",
                    function = new { name = "EnterpriseSearch" }
                }
            }
        });

        // When
        var exception = Record.Exception(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Null(exception);
    }

    [Theory, AutoDomainData]
    public void Given_MissingEnterpriseSearch_When_Validate_Then_RejectsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new { tools = Array.Empty<object>() });

        // When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Contains("EnterpriseSearch", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public void Given_WebSearchAlongsideEnterpriseSearch_When_Validate_Then_RejectsDefinition()
    {
        // Given
        var definition = JsonSerializer.SerializeToElement(new
        {
            tools = new[]
            {
                new { type = "function", name = "EnterpriseSearch" },
                new { type = "web_search", name = string.Empty }
            }
        });

        // When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FoundryAgentDefinitionValidator.Validate(definition));

        // Then
        Assert.Contains("web_search", exception.Message, StringComparison.Ordinal);
    }
}
