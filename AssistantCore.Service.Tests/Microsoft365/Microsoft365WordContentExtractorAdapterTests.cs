using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365WordContentExtractorAdapterTests
{
    [Theory]
    [InlineAutoDomainData(".doc")]
    [InlineAutoDomainData(".docx")]
    [InlineAutoDomainData(".docm")]
    public void Given_AWordExtension_When_CanExtract_Then_ReturnsTrue(
        string extension,
        string fileName)
    {
        // Given
        var adapter = CreateAdapter();

        // When
        var canExtract = adapter.CanExtract($"{fileName}{extension}", null);

        // Then
        Assert.True(canExtract);
    }

    [Theory, AutoDomainData]
    public async Task Given_AUnsupportedLegacyDocument_When_ExtractAsync_Then_MapsUnsupportedFormat(
        string fileName,
        byte[] content)
    {
        // Given
        var adapter = CreateAdapter();
        var request = new Microsoft365ContentExtractionRequest(
            $"{fileName}.doc",
            "application/msword",
            new MemoryStream(content),
            content.Length);

        // When
        var result = await adapter.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ContentExtractionStatus.UnsupportedFormat, result.Status);
    }

    private static Microsoft365WordContentExtractorAdapter CreateAdapter() =>
        new(
            new MicrosoftWordContentExtractorClient(),
            Options.Create(new Microsoft365Options()));
}
