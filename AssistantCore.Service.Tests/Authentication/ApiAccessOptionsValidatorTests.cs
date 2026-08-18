using AssistantCore.Service.Infrastructure.Authentication.Configuration;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class ApiAccessOptionsValidatorTests
{
    [Fact]
    public void Given_AConfiguredScope_When_Validate_Then_ValidationSucceeds()
    {
        // Given
        var options = new ApiAccessOptions { RequiredScope = "access_as_user" };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_AMissingScope_When_Validate_Then_ValidationFailsOnTheScopeKey(string scope)
    {
        // Given
        var options = new ApiAccessOptions { RequiredScope = scope };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains("AzureAd:RequiredScope is required.", result.Failures);
    }

    [Fact]
    public void Given_AScopeContainingSeveralValues_When_Validate_Then_ValidationFails()
    {
        // Given
        // Une liste de scopes ici rendrait l'exigence impossible a satisfaire silencieusement.
        var options = new ApiAccessOptions { RequiredScope = "access_as_user profile" };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            "AzureAd:RequiredScope must be a single value without whitespace.",
            result.Failures);
    }
}
