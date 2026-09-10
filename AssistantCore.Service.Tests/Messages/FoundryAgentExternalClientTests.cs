using AssistantCore.ExternalServices.Services.Foundry;

namespace AssistantCore.Service.Tests.Messages;

public sealed class FoundryAgentExternalClientTests
{
    [Theory]
    [InlineAutoDomainData(" ")]
    [InlineAutoDomainData("\n")]
    public void Given_WhitespaceFragment_When_HasStreamableText_Then_ReturnsTrue(string fragment)
    {
        // Given
        // The fragment contains formatting required to reconstruct the streamed answer.

        // When
        var result = FoundryAgentExternalClient.HasStreamableText(fragment);

        // Then
        Assert.True(result);
    }
}
