using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365PlainTextContentExtractorAdapterTests
{
    [Theory]
    [InlineAutoDomainData(".txt")]
    [InlineAutoDomainData(".md")]
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
        var canExtract = adapter.CanExtract($"{fileName}.docx", null);

        // Then
        Assert.False(canExtract);
    }

    [Theory, AutoDomainData]
    public async Task Given_ATextFile_When_ExtractAsync_Then_MapsToSuccessWithAParagraphUnit(string fileName)
    {
        // Given
        var adapter = CreateAdapter();
        var request = new Microsoft365ContentExtractionRequest(
            $"{fileName}.txt",
            "text/plain",
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Bonjour")),
            7);

        // When
        var result = await adapter.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ContentExtractionStatus.Success, result.Status);
        var unit = Assert.Single(result.Units);
        Assert.Equal(Microsoft365ExtractedContentUnitKind.Paragraph, unit.Kind);
        Assert.Equal("Bonjour", unit.Text);
    }

    private static Microsoft365PlainTextContentExtractorAdapter CreateAdapter() =>
        new(
            new MicrosoftPlainTextContentExtractorClient(),
            Options.Create(new Microsoft365Options()));
}
