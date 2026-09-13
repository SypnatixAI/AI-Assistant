using System.Security.Cryptography;
using System.Text;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ClientStateProtectorAdapterTests
{
    [Theory, AutoDomainData]
    public void Given_AProtectedClientState_When_Matches_Then_OnlyTheOriginalSecretMatches(
        string otherClientState)
    {
        // Given
        var protector = CreateProtector();
        var clientState = protector.Create();

        // When
        var originalMatches = protector.Matches(clientState.Value, clientState.ProtectedValue);
        var otherMatches = protector.Matches(otherClientState, clientState.ProtectedValue);

        // Then
        Assert.True(originalMatches);
        Assert.False(otherMatches);
        Assert.DoesNotContain(clientState.Value, clientState.ProtectedValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnEmptyClientState_When_Matches_Then_OnlyAnEmptyValueMatchesItsOwnProtectedValue()
    {
        // Given
        const string hmacKey = "test-hmac-key-at-least-32-characters-long";
        var protector = CreateProtector(hmacKey);
        var protectedEmpty = ComputeExpectedHex(string.Empty, hmacKey);

        // When
        var emptyMatchesItsOwnHash = protector.Matches(string.Empty, protectedEmpty);
        var nonEmptyMatchesEmptyHash = protector.Matches("not-empty", protectedEmpty);

        // Then
        Assert.True(emptyMatchesItsOwnHash);
        Assert.False(nonEmptyMatchesEmptyHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-hex-at-all")]
    [InlineData("00")]
    public void Given_AMalformedOrLegacyProtectedValue_When_Matches_Then_ReturnsFalse(string protectedValue)
    {
        // Given
        var protector = CreateProtector();
        var clientState = protector.Create();

        // When
        var matches = protector.Matches(clientState.Value, protectedValue);

        // Then
        Assert.False(matches);
    }

    [Fact]
    public void Given_TwoDifferentHmacKeys_When_Matches_Then_TheOtherKeyNeverMatches()
    {
        // Given
        var protector = CreateProtector("first-hmac-key-at-least-32-characters-long");
        var otherKeyProtector = CreateProtector("second-hmac-key-at-least-32-characters-long");
        var clientState = protector.Create();

        // When
        var matchesWithOtherKey = otherKeyProtector.Matches(clientState.Value, clientState.ProtectedValue);

        // Then
        Assert.False(matchesWithOtherKey);
    }

    private static string ComputeExpectedHex(string value, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static Microsoft365ClientStateProtectorAdapter CreateProtector(
        string hmacKey = "test-hmac-key-at-least-32-characters-long") =>
        new(Options.Create(new Microsoft365Options { ClientStateHmacKey = hmacKey }));
}
