using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftXmlContentExtractorClientTests
{
    private const long MaximumFileSize = 1_000_000;
    private const int MaximumDepth = 32;
    private const int MaximumCharacters = 100_000;

    [Theory, AutoDomainData]
    public async Task Given_NestedElements_When_ExtractAsync_Then_ReturnsDotPathsForEachLeaf(string fileName)
    {
        // Given
        var xml = """<?xml version="1.0" encoding="UTF-8"?><client><name>Contoso</name><active>true</active></client>""";
        await using var content = ToStream(xml);
        var client = new MicrosoftXmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "application/xml", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Equal(
            ["client.name : Contoso", "client.active : true"],
            result.Units.Select(unit => unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_RepeatedSiblingElements_When_ExtractAsync_Then_IndexesEachOccurrence(string fileName)
    {
        // Given
        var xml = """<?xml version="1.0" encoding="UTF-8"?><items><item><sku>A1</sku></item><item><sku>B2</sku></item></items>""";
        await using var content = ToStream(xml);
        var client = new MicrosoftXmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "application/xml", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(
            ["items.item[0].sku : A1", "items.item[1].sku : B2"],
            result.Units.Select(unit => unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_AnExternalEntityDeclaration_When_ExtractAsync_Then_ReturnsCorruptedDocumentWithoutResolvingIt(
        string fileName)
    {
        // Given
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE root [ <!ENTITY xxe SYSTEM "file:///etc/passwd"> ]>
            <root>&xxe;</root>
            """;
        await using var content = ToStream(xml);
        var client = new MicrosoftXmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "application/xml", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_MalformedXml_When_ExtractAsync_Then_ReturnsCorruptedDocument(string fileName)
    {
        // Given
        await using var content = ToStream("<root><unclosed></root>");
        var client = new MicrosoftXmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "application/xml", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyFile_When_ExtractAsync_Then_ReturnsEmptyDocument(string fileName)
    {
        // Given
        await using var content = ToStream(string.Empty);
        var client = new MicrosoftXmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "application/xml", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.EmptyDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongExtension_When_ExtractAsync_Then_ReturnsUnsupportedFormat(string fileName)
    {
        // Given
        await using var content = ToStream("<root><a>1</a></root>");
        var client = new MicrosoftXmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "application/xml", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.UnsupportedFormat, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_ElementsDeeperThanTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        var xml = "<a><b><c><d><e>valeur</e></d></c></b></a>";
        await using var content = ToStream(xml);
        var client = new MicrosoftXmlContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "application/xml", content, content.Length, MaximumFileSize, maximumDepth: 2, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));
}
