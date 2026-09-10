using System.IO.Compression;
using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftOpenDocumentContentExtractorClientTests
{
    private const long MaximumFileSize = 1_000_000;
    private const long MaximumExpandedSize = 2_000_000;
    private const int MaximumCharacters = 100_000;

    [Theory, AutoDomainData]
    public async Task Given_AnOdtDocument_When_ExtractAsync_Then_ExtractsHeadingsAndParagraphs(string fileName)
    {
        // Given
        var contentXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
              <office:body>
                <office:text>
                  <text:h>Politique de télétravail</text:h>
                  <text:p>Les employés peuvent travailler à distance.</text:p>
                </office:text>
              </office:body>
            </office:document-content>
            """;
        await using var package = CreatePackage("application/vnd.oasis.opendocument.text", contentXml);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.odt", null, package, package.Length, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Collection(
            result.Units,
            unit => Assert.True(unit is { IsTitle: true, Text: "Politique de télétravail" }),
            unit => Assert.True(unit is { IsTitle: false, Text: "Les employés peuvent travailler à distance." }));
    }

    [Theory, AutoDomainData]
    public async Task Given_AnOdsSpreadsheet_When_ExtractAsync_Then_ExtractsHeaderAndRows(string fileName)
    {
        // Given
        var contentXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
              <office:body>
                <office:spreadsheet>
                  <table:table table:name="Budget">
                    <table:table-row>
                      <table:table-cell><text:p>Nom</text:p></table:table-cell>
                      <table:table-cell><text:p>Montant</text:p></table:table-cell>
                    </table:table-row>
                    <table:table-row>
                      <table:table-cell><text:p>Écran</text:p></table:table-cell>
                      <table:table-cell><text:p>450</text:p></table:table-cell>
                    </table:table-row>
                  </table:table>
                </office:spreadsheet>
              </office:body>
            </office:document-content>
            """;
        await using var package = CreatePackage("application/vnd.oasis.opendocument.spreadsheet", contentXml);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.ods", null, package, package.Length, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Collection(
            result.Units,
            unit => Assert.Equal("Feuille : Budget", unit.Text),
            unit => Assert.Equal("Nom : Écran | Montant : 450", unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_AnOdpPresentation_When_ExtractAsync_Then_ExtractsSlidesAndText(string fileName)
    {
        // Given
        var contentXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
              <office:body>
                <office:presentation>
                  <draw:page draw:name="Introduction">
                    <draw:frame><draw:text-box><text:p>Bienvenue</text:p></draw:text-box></draw:frame>
                  </draw:page>
                </office:presentation>
              </office:body>
            </office:document-content>
            """;
        await using var package = CreatePackage("application/vnd.oasis.opendocument.presentation", contentXml);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.odp", null, package, package.Length, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Collection(
            result.Units,
            unit => Assert.Equal("Diapositive 1 : Introduction", unit.Text),
            unit => Assert.Equal("Bienvenue", unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_AFalsifiedExtension_When_ExtractAsync_Then_ReturnsCorruptedDocument(string fileName)
    {
        // Given
        // Le fichier est nomme .odt mais son entree mimetype declare un tableur : le
        // routeur ne doit jamais faire confiance a la seule extension.
        var contentXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0">
              <office:body/>
            </office:document-content>
            """;
        await using var package = CreatePackage("application/vnd.oasis.opendocument.spreadsheet", contentXml);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.odt", null, package, package.Length, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyDocument_When_ExtractAsync_Then_ReturnsEmptyDocument(string fileName)
    {
        // Given
        var contentXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-content
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
              <office:body><office:text/></office:body>
            </office:document-content>
            """;
        await using var package = CreatePackage("application/vnd.oasis.opendocument.text", contentXml);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.odt", null, package, package.Length, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.EmptyDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidPackage_When_ExtractAsync_Then_ReturnsCorruptedDocument(
        string fileName,
        byte[] invalidContent)
    {
        // Given
        await using var content = new MemoryStream([0x01, 0x02, 0x03, 0x04, .. invalidContent]);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.odt", null, content, content.Length, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongExtension_When_ExtractAsync_Then_ReturnsUnsupportedFormat(string fileName)
    {
        // Given
        var contentXml = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body/></office:document-content>";
        await using var package = CreatePackage("application/vnd.oasis.opendocument.text", contentXml);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.docx", null, package, package.Length, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.UnsupportedFormat, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFileAboveTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        var contentXml = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body/></office:document-content>";
        await using var package = CreatePackage("application/vnd.oasis.opendocument.text", contentXml);
        var client = new MicrosoftOpenDocumentContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.odt", null, package, MaximumFileSize + 1, MaximumFileSize, MaximumExpandedSize, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

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
