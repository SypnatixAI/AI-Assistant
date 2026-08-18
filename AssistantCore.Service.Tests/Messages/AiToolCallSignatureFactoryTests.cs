using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Orchestration;

namespace AssistantCore.Service.Tests.Messages;

public sealed class ToolCallFingerprintGeneratorTests
{
    [Theory, AutoDomainData]
    public void Given_EquivalentArgumentsInDifferentPropertyOrder_When_CreateFingerprint_Then_ReturnsTheSameFingerprint(
        Guid firstCallId,
        Guid secondCallId)
    {
        // Given
        var firstCall = new AiRequestedToolCall(
            firstCallId.ToString(),
            AiToolNames.SearchInternalData,
            ParseJson("""{"query":"sales","page":1}"""));
        var secondCall = new AiRequestedToolCall(
            secondCallId.ToString(),
            AiToolNames.SearchInternalData,
            ParseJson("""{"page":1,"query":"sales"}"""));
        var generator = new ToolCallFingerprintGenerator();

        // When
        var firstFingerprint = generator.CreateFingerprint(firstCall);
        var secondFingerprint = generator.CreateFingerprint(secondCall);

        // Then
        Assert.Equal(firstFingerprint, secondFingerprint);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
