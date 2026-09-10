using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftHtmlContentExtractorClientTests
{
    private const long MaximumFileSize = 1_000_000;
    private const int MaximumCharacters = 100_000;

    [Theory, AutoDomainData]
    public async Task Given_HeadingsAndParagraphs_When_ExtractAsync_Then_ExtractsVisibleTextInOrder(string fileName)
    {
        // Given
        var html = """
            <html><body>
              <h1>Bienvenue</h1>
              <p>Ceci est un paragraphe.</p>
              <h2>Section</h2>
              <p>Un autre paragraphe.</p>
            </body></html>
            """;
        await using var content = ToStream(html);
        var client = new MicrosoftHtmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.html", "text/html", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Contains(result.Units, unit => unit is { IsTitle: true, Text: "Bienvenue" });
        Assert.Contains(result.Units, unit => unit is { IsTitle: true, Text: "Section" });
        Assert.Contains(result.Units, unit => unit is { IsTitle: false, Text: "Ceci est un paragraphe." });
        Assert.Contains(result.Units, unit => unit is { IsTitle: false, Text: "Un autre paragraphe." });
    }

    [Theory, AutoDomainData]
    public async Task Given_ScriptAndStyleTags_When_ExtractAsync_Then_NeverIncludesTheirContent(string fileName)
    {
        // Given
        var html = """
            <html><head><style>body { color: red; }</style></head>
            <body>
              <script>alert('should never run or appear');</script>
              <p>Texte visible.</p>
            </body></html>
            """;
        await using var content = ToStream(html);
        var client = new MicrosoftHtmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.html", "text/html", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.DoesNotContain(result.Units, unit => unit.Text.Contains("alert", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Units, unit => unit.Text.Contains("color: red", StringComparison.Ordinal));
        Assert.Contains(result.Units, unit => unit.Text == "Texte visible.");
    }

    [Theory, AutoDomainData]
    public async Task Given_HtmlEntities_When_ExtractAsync_Then_DecodesThem(string fileName)
    {
        // Given
        var html = "<p>Caf&eacute; &amp; Th&eacute;</p>";
        await using var content = ToStream(html);
        var client = new MicrosoftHtmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.html", "text/html", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal("Café & Thé", Assert.Single(result.Units).Text);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyFile_When_ExtractAsync_Then_ReturnsEmptyDocument(string fileName)
    {
        // Given
        await using var content = ToStream(string.Empty);
        var client = new MicrosoftHtmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.html", "text/html", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.EmptyDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongExtension_When_ExtractAsync_Then_ReturnsUnsupportedFormat(string fileName)
    {
        // Given
        await using var content = ToStream("<p>contenu</p>");
        var client = new MicrosoftHtmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "text/html", content, content.Length, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.UnsupportedFormat, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFileAboveTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        await using var content = ToStream("<p>contenu</p>");
        var client = new MicrosoftHtmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.html", "text/html", content, MaximumFileSize + 1, MaximumFileSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));
}
