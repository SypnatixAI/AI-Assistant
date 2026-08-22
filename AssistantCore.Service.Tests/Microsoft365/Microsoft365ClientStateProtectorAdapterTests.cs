using AssistantCore.Service.Infrastructure.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ClientStateProtectorAdapterTests
{
    [Theory, AutoDomainData]
    public void Given_AProtectedClientState_When_Matches_Then_OnlyTheOriginalSecretMatches(
        string otherClientState)
    {
        // Given
        var protector = new Microsoft365ClientStateProtectorAdapter();
        var clientState = protector.Create();

        // When
        var originalMatches = protector.Matches(clientState.Value, clientState.ProtectedValue);
        var otherMatches = protector.Matches(otherClientState, clientState.ProtectedValue);

        // Then
        Assert.True(originalMatches);
        Assert.False(otherMatches);
        Assert.DoesNotContain(clientState.Value, clientState.ProtectedValue, StringComparison.Ordinal);
    }
}
