using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365DocumentIndexVersionTests
{
    [Theory, AutoDomainData]
    public void Given_ADocumentETag_When_Create_Then_IncludesTheCurrentPipelineVersion(
        string documentETag)
    {
        // Given

        // When
        var version = Microsoft365DocumentIndexVersion.Create(documentETag);

        // Then
        Assert.Equal($"2:{documentETag}", version);
    }
}
