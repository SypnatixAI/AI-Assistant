using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftPlainTextContentExtractorClientTests
{
    private const long MaximumFileSize = 1_000_000;
    private const int MaximumCharacters = 100_000;

    [Theory, AutoDomainData]
    public async Task Given_ATxtFileWithParagraphs_When_ExtractAsync_Then_ReturnsOneUnitPerParagraph(
        string fileName)
    {
        // Given
        var text = "Premier paragraphe.\n\nDeuxieme   paragraphe avec des espaces.";
        await using var content = ToStream(text);
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.txt", "text/plain", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Equal(
            ["Premier paragraphe.", "Deuxieme paragraphe avec des espaces."],
            result.Units.Select(unit => unit.Text));
        Assert.All(result.Units, unit => Assert.False(unit.IsTitle));
    }

    [Theory, AutoDomainData]
    public async Task Given_AMarkdownHeading_When_ExtractAsync_Then_MarksItAsTitle(
        string fileName)
    {
        // Given
        var text = "# Titre principal\n\nUn paragraphe normal.\n\n## Sous-titre";
        await using var content = ToStream(text);
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.md", "text/markdown", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Collection(
            result.Units,
            unit => Assert.True(unit.IsTitle && unit.Text == "Titre principal"),
            unit => Assert.True(!unit.IsTitle && unit.Text == "Un paragraphe normal."),
            unit => Assert.True(unit.IsTitle && unit.Text == "Sous-titre"));
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyFile_When_ExtractAsync_Then_ReturnsEmptyDocument(string fileName)
    {
        // Given
        await using var content = ToStream(string.Empty);
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.txt", "text/plain", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.EmptyDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongExtension_When_ExtractAsync_Then_ReturnsUnsupportedFormat(string fileName)
    {
        // Given
        await using var content = ToStream("contenu");
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.docx", "text/plain", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.UnsupportedFormat, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AMismatchedMimeType_When_ExtractAsync_Then_ReturnsUnsupportedFormat(string fileName)
    {
        // Given
        await using var content = ToStream("contenu");
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.txt",
            "application/vnd.ms-excel",
            content,
            content.Length,
            MaximumFileSize,
            MaximumCharacters,
            CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.UnsupportedFormat, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFalsifiedExtensionOnBinaryContent_When_ExtractAsync_Then_ReturnsCorruptedDocument(
        string fileName)
    {
        // Given
        var binary = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00 };
        await using var content = new MemoryStream(binary);
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.txt", null, content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFileAboveTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        await using var content = ToStream("contenu");
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.txt", null, content, MaximumFileSize + 1, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_ContentAboveTheCharacterLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        var text = new string('a', 10);
        await using var content = ToStream(text);
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.txt", null, content, content.Length, MaximumFileSize, maximumExtractedCharacters: 5, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AUtf8BomFile_When_ExtractAsync_Then_StripsTheBomFromTheText(string fileName)
    {
        // Given
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Bonjour")];
        await using var content = new MemoryStream(bytes);
        var client = new MicrosoftPlainTextContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.txt", null, content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Equal("Bonjour", Assert.Single(result.Units).Text);
    }

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));
}
