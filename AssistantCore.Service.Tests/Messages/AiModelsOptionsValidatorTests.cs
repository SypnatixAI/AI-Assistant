using AssistantCore.Service.Infrastructure.AiModels.Configuration;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiModelsOptionsValidatorTests
{
    [Fact]
    public void Given_AValidOpenAiConfiguration_When_Validate_Then_ReturnsSuccess()
    {
        // Given
        var options = CreateValidOptions();
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Given_AnEnabledProviderWithoutApiKey_When_Validate_Then_ReturnsFailureWithoutSecretValue()
    {
        // Given
        var options = CreateValidOptions(openAiApiKey: string.Empty);
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("AiModels:Providers:OpenAI:ApiKey", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnInvalidConfigurationContainingASecret_When_Validate_Then_DoesNotExposeTheSecret()
    {
        // Given
        const string secret = "secret-that-must-never-appear";
        var options = CreateValidOptions(
            openAiEndpoint: "http://api.openai.com/v1",
            openAiApiKey: secret);
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.DoesNotContain(
            result.Failures,
            failure => failure.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AnInvalidDefaultModel_When_Validate_Then_ReturnsFailure()
    {
        // Given
        var options = CreateValidOptions(defaultModel: "gpt-unknown");
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("DefaultModel", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AMissingDefaultModel_When_Validate_Then_ReturnsFailure()
    {
        // Given
        var options = CreateValidOptions(defaultModel: string.Empty);
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("AiModels:DefaultModel is required.", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AnInactiveDefaultModel_When_Validate_Then_ReturnsFailure()
    {
        // Given
        var options = CreateValidOptions();
        options.Providers["OpenAI"].Models["gpt-5.6-luna"] = new AiModelOptions
        {
            Enabled = false
        };
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "AiModels:DefaultModel must reference an active configured model.",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Given_NoActiveModel_When_Validate_Then_ReturnsFailure()
    {
        // Given
        var options = CreateValidOptions();
        options.Providers["OpenAI"].Models.Clear();
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "At least one model must be active for an enabled provider.",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AnInvalidProviderEndpoint_When_Validate_Then_ReturnsFailure()
    {
        // Given
        var options = CreateValidOptions(openAiEndpoint: "http://api.openai.com/v1");
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("Endpoint", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_ANonPositiveTimeout_When_Validate_Then_ReturnsFailure(
        int timeoutSeconds)
    {
        // Given
        var options = CreateValidOptions(timeoutSeconds: timeoutSeconds);
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "AiModels:Providers:OpenAI:TimeoutSeconds must be greater than zero.",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Given_ADuplicateActiveModel_When_Validate_Then_ReturnsFailure()
    {
        // Given
        var options = CreateValidOptions();
        options.Providers.Add(
            "AnotherProvider",
            new AiModelProviderOptions
            {
                Enabled = true,
                Endpoint = "https://example.com/v1",
                ApiKey = "another-secret",
                TimeoutSeconds = 30,
                Models = new Dictionary<string, AiModelOptions>
                {
                    ["GPT-5.6-LUNA"] = new() { Enabled = true }
                }
            });
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("more than one provider", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AnUnconfiguredDisabledAnthropicProvider_When_Validate_Then_ReturnsSuccess()
    {
        // Given
        var options = CreateValidOptions();
        options.Providers.Add("Anthropic", new AiModelProviderOptions { Enabled = false });
        var validator = new AiModelsOptionsValidator();

        // When
        var result = validator.Validate(null, options);

        // Then
        Assert.True(result.Succeeded);
    }

    private static AiModelsOptions CreateValidOptions(
        string defaultModel = "gpt-5.6-luna",
        string openAiEndpoint = "https://api.openai.com/v1",
        string openAiApiKey = "test-secret",
        int timeoutSeconds = 60) =>
        new()
        {
            DefaultModel = defaultModel,
            Providers = new Dictionary<string, AiModelProviderOptions>
            {
                ["OpenAI"] = new()
                {
                    Enabled = true,
                    Endpoint = openAiEndpoint,
                    ApiKey = openAiApiKey,
                    TimeoutSeconds = timeoutSeconds,
                    Models = new Dictionary<string, AiModelOptions>
                    {
                        ["gpt-5.6-luna"] = new() { Enabled = true },
                        ["gpt-5.6-terra"] = new() { Enabled = true },
                        ["gpt-5.6-sol"] = new() { Enabled = true }
                    }
                }
            }
        };
}
