using AssistantCore.ExternalServices.Services.Foundry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

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

    [Theory, AutoDomainData]
    public void Given_TextAndToolResult_When_GetStreamableText_Then_ReturnsTheTextFragment()
    {
        // Given
        const string fragment = "Voici la réponse";
        var update = new AgentResponseUpdate(
            ChatRole.Assistant,
            [
                new TextContent(fragment),
                new FunctionResultContent("tool-call-id", new { status = "succeeded" })
            ]);

        // When
        var result = FoundryAgentExternalClient.GetStreamableText(update);

        // Then
        Assert.Equal(fragment, result);
    }
}
