using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Integration;

public sealed class ApplicationStartupTests
{
    [Theory]
    [InlineAutoDomainData("MaximumExecutionTimeSeconds")]
    [InlineAutoDomainData("MaximumToolCalls")]
    [InlineAutoDomainData("MaximumModelTokens")]
    [InlineAutoDomainData("MaximumEstimatedCost")]
    [InlineAutoDomainData("MaximumResultsPerTool")]
    [InlineAutoDomainData("MaximumContextSize")]
    [InlineAutoDomainData("MaximumRepeatedToolCalls")]
    [InlineAutoDomainData("MaximumParallelToolCalls")]
    public void Given_AnInvalidOrchestrationLimit_When_CreateClient_Then_StartupFails(
        string optionName)
    {
        // Given
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            [$"Messages:Orchestration:{optionName}"] = "0",
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret"
                        }));
            });

        // When
        var exception = Assert.Throws<OptionsValidationException>(() =>
            factory.CreateClient());

        // Then
        Assert.Contains(
            "Messages:Orchestration",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineAutoDomainData(0)]
    [InlineAutoDomainData(-1)]
    public void Given_AnInvalidMaximumMessageLength_When_CreateClient_Then_StartupFailsWithTheInvalidField(
        int maximumMessageLength)
    {
        // Given
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Messages:MaximumMessageLength"] = maximumMessageLength.ToString(),
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret"
                        }));
            });

        // When
        var exception = Assert.Throws<OptionsValidationException>(() =>
            factory.CreateClient());

        // Then
        Assert.Contains(
            "Messages:MaximumMessageLength",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnInvalidAiModelConfiguration_When_CreateClient_Then_StartupFailsWithTheInvalidField()
    {
        // Given
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["AiModels:Providers:OpenAI:ApiKey"] = string.Empty
                        }));
            });

        // When
        var exception = Assert.Throws<OptionsValidationException>(() =>
            factory.CreateClient());

        // Then
        Assert.Contains(
            "AiModels:Providers:OpenAI:ApiKey",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ValidDevelopmentConfiguration_When_StartingApplication_Then_RootEndpointRespondsWithoutError()
    {
        // Given
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret"
                        }));
            });

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        // When
        using var response = await client.GetAsync("/");

        // Then
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.OriginalString);
    }
}
