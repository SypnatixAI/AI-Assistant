using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Infrastructure.AiModels;
using AssistantCore.Service.Infrastructure.AiModels.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AuthorizedAiModelSelectorTests
{
    [Fact]
    public async Task Given_NoRequestedModel_When_SelectAsync_Then_ReturnsTheDefaultModel()
    {
        // Given
        var selector = CreateSelector();

        // When
        var result = await selector.SelectAsync(
            Guid.NewGuid(),
            null,
            CancellationToken.None);

        // Then
        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal("gpt-5.6-luna", result.ModelName);
    }

    [Theory]
    [InlineData("gpt-5.6-luna")]
    [InlineData("gpt-5.6-terra")]
    [InlineData("gpt-5.6-sol")]
    public async Task Given_AnActiveRequestedModel_When_SelectAsync_Then_ReturnsItsCanonicalConfiguration(
        string requestedModel)
    {
        // Given
        var selector = CreateSelector();

        // When
        var result = await selector.SelectAsync(
            Guid.NewGuid(),
            requestedModel.ToUpperInvariant(),
            CancellationToken.None);

        // Then
        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal(requestedModel, result.ModelName);
    }

    [Theory]
    [InlineData("gpt-unknown")]
    [InlineData("gpt-disabled")]
    [InlineData("claude-disabled")]
    public async Task Given_AnUnavailableRequestedModel_When_SelectAsync_Then_ThrowsBadRequest(
        string requestedModel)
    {
        // Given
        var selector = CreateSelector();

        // When
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            selector.SelectAsync(
                Guid.NewGuid(),
                requestedModel,
                CancellationToken.None));

        // Then
        Assert.Equal("The requested AI model is not available.", exception.Message);
    }

    [Fact]
    public async Task Given_AnEmptyOrganizationIdentifier_When_SelectAsync_Then_ThrowsBeforeSelection()
    {
        // Given
        var selector = CreateSelector();

        // When
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            selector.SelectAsync(
                Guid.Empty,
                "gpt-5.6-luna",
                CancellationToken.None));

        // Then
        Assert.Equal("organizationId", exception.ParamName);
    }

    [Fact]
    public async Task Given_ACancelledRequest_When_SelectAsync_Then_PropagatesCancellation()
    {
        // Given
        var selector = CreateSelector();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // When
        var exception = await Record.ExceptionAsync(() =>
            selector.SelectAsync(
                Guid.NewGuid(),
                "gpt-5.6-luna",
                cancellationTokenSource.Token));

        // Then
        Assert.IsType<OperationCanceledException>(exception);
    }

    [Theory]
    [InlineAutoDomainData("", true)]
    [InlineAutoDomainData("gpt-5.6-luna", true)]
    [InlineAutoDomainData("  GPT-5.6-TERRA  ", true)]
    [InlineAutoDomainData("gpt-disabled", false)]
    [InlineAutoDomainData("gpt-unknown", false)]
    public void Given_ARequestedModel_When_IsAvailable_Then_ReturnsItsConfigurationStatus(
        string? requestedModel,
        bool expectedAvailability)
    {
        // Given
        var selector = CreateSelector();

        // When
        var isAvailable = selector.IsAvailable(requestedModel);

        // Then
        Assert.Equal(expectedAvailability, isAvailable);
    }

    private static AuthorizedAiModelSelector CreateSelector()
    {
        var options = new AiModelsOptions
        {
            DefaultModel = "gpt-5.6-luna",
            Providers = new Dictionary<string, AiModelProviderOptions>
            {
                ["OpenAI"] = new()
                {
                    Enabled = true,
                    Models = new Dictionary<string, AiModelOptions>
                    {
                        ["gpt-5.6-luna"] = new() { Enabled = true },
                        ["gpt-5.6-terra"] = new() { Enabled = true },
                        ["gpt-5.6-sol"] = new() { Enabled = true },
                        ["gpt-disabled"] = new() { Enabled = false }
                    }
                },
                ["Anthropic"] = new()
                {
                    Enabled = false,
                    Models = new Dictionary<string, AiModelOptions>
                    {
                        ["claude-disabled"] = new() { Enabled = true }
                    }
                }
            }
        };

        return new AuthorizedAiModelSelector(Options.Create(options));
    }
}
