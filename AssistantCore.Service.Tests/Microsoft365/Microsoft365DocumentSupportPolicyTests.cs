using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365DocumentSupportPolicyTests
{
    [Theory, AutoDomainData]
    public void Given_AKnownAudioMimeType_When_IsSupported_Then_ReturnsFalse(string fileName)
    {
        // Given
        var policy = new Microsoft365DocumentSupportPolicy();

        // When
        var isSupported = policy.IsSupported(fileName, "audio/mpeg");

        // Then
        Assert.False(isSupported);
    }

    [Theory, AutoDomainData]
    public void Given_AnUnknownDocumentFormat_When_IsSupported_Then_ReturnsTrue(string extension)
    {
        // Given
        var policy = new Microsoft365DocumentSupportPolicy();
        var fileName = $"document.{extension}.custom";

        // When
        var isSupported = policy.IsSupported(fileName, "application/octet-stream");

        // Then
        Assert.True(isSupported);
    }
}
