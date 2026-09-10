using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365HtmlContentExtractorAdapterTests
{
    [Theory]
    [InlineAutoDomainData(".html")]
    [InlineAutoDomainData(".htm")]
    public void Given_ASupportedExtension_When_CanExtract_Then_ReturnsTrue(string extension, string fileName)
    {
        // Given
        var adapter = CreateAdapter();

        // When
        var canExtract = adapter.CanExtract($"{fileName}{extension}", null);

        // Then
        Assert.True(canExtract);
    }

    [Theory, AutoDomainData]
    public void Given_AnUnsupportedExtension_When_CanExtract_Then_ReturnsFalse(string fileName)
    {
        // Given
        var adapter = CreateAdapter();

        // When
        var canExtract = adapter.CanExtract($"{fileName}.xml", null);

        // Then
        Assert.False(canExtract);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnHtmlFile_When_ExtractAsync_Then_MapsToSuccessWithATitleUnit(string fileName)
    {
        // Given
        var adapter = CreateAdapter();
        var bytes = System.Text.Encoding.UTF8.GetBytes("<h1>Bienvenue</h1>");
        var request = new Microsoft365ContentExtractionRequest(
            $"{fileName}.html",
            "text/html",
            new MemoryStream(bytes),
            bytes.Length);

        // When
        var result = await adapter.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ContentExtractionStatus.Success, result.Status);
        var unit = Assert.Single(result.Units);
        Assert.Equal(Microsoft365ExtractedContentUnitKind.Title, unit.Kind);
        Assert.Equal("Bienvenue", unit.Text);
    }

    private static Microsoft365HtmlContentExtractorAdapter CreateAdapter() =>
        new(
            new MicrosoftHtmlContentExtractorClient(),
            Options.Create(new Microsoft365Options()));
}
