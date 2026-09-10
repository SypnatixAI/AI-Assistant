using System.IO.Compression;
using System.Text;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365OpenDocumentContentExtractorAdapterTests
{
    [Theory]
    [InlineAutoDomainData(".odt")]
    [InlineAutoDomainData(".ods")]
    [InlineAutoDomainData(".odp")]
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
    public async Task Given_AnOdtFile_When_ExtractAsync_Then_MapsToSuccessWithAParagraphUnit(string fileName)
    {
        // Given
        var adapter = CreateAdapter();
        var contentXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
              <office:body><office:text><text:p>Bonjour</text:p></office:text></office:body>
            </office:document-content>
            """;
        await using var package = CreatePackage("application/vnd.oasis.opendocument.text", contentXml);
        var request = new Microsoft365ContentExtractionRequest($"{fileName}.odt", null, package, package.Length);

        // When
        var result = await adapter.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ContentExtractionStatus.Success, result.Status);
        var unit = Assert.Single(result.Units);
        Assert.Equal(Microsoft365ExtractedContentUnitKind.Paragraph, unit.Kind);
        Assert.Equal("Bonjour", unit.Text);
    }

    private static Microsoft365OpenDocumentContentExtractorAdapter CreateAdapter() =>
        new(
            new MicrosoftOpenDocumentContentExtractorClient(),
            Options.Create(new Microsoft365Options()));

    private static MemoryStream CreatePackage(string mimeType, string contentXml)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimetypeEntry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(mimeType);
            }

            var contentEntry = archive.CreateEntry("content.xml");
            using (var writer = new StreamWriter(contentEntry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(contentXml);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
