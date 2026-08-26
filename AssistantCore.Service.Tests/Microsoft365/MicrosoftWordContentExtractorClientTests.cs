using System.IO.Compression;
using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftWordContentExtractorClientTests
{
    private const long MaximumFileSize = 1_000_000;
    private const long MaximumExpandedSize = 2_000_000;
    private const int MaximumCharacters = 100_000;

    [Theory, AutoDomainData]
    public async Task Given_ParagraphsAListAndATable_When_ExtractAsync_Then_ReturnsStableStructuredText(
        string fileName)
    {
        // Given
        var documentXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                <w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Politique de télétravail</w:t></w:r></w:p>
                <w:p><w:r><w:t xml:space="preserve">  Les employés   peuvent travailler à distance… </w:t></w:r></w:p>
                <w:p><w:pPr><w:numPr><w:numId w:val="1"/></w:numPr></w:pPr><w:r><w:t>Premier élément</w:t></w:r></w:p>
                <w:tbl>
                  <w:tr><w:tc><w:p><w:r><w:t>Jour</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Présence requise</w:t></w:r></w:p></w:tc></w:tr>
                  <w:tr><w:tc><w:p><w:r><w:t>Mardi</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Oui</w:t></w:r></w:p></w:tc></w:tr>
                </w:tbl>
              </w:body>
            </w:document>
            """;
        await using var firstPackage = CreatePackage(documentXml);
        await using var secondPackage = CreatePackage(documentXml);
        var client = new MicrosoftWordContentExtractorClient();

        // When
        var first = await ExtractAsync(client, firstPackage, fileName);
        var second = await ExtractAsync(client, secondPackage, fileName);

        // Then
        Assert.Equal(MicrosoftWordExtractionStatus.Success, first.Status);
        Assert.Equal(first.Units, second.Units);
        Assert.Collection(
            first.Units,
            unit => Assert.Equal(MicrosoftWordExtractedUnitKind.Title, unit.Kind),
            unit => Assert.Equal("Les employés peuvent travailler à distance…", unit.Text),
            unit => Assert.Equal(MicrosoftWordExtractedUnitKind.ListItem, unit.Kind),
            unit => Assert.Equal("Tableau", unit.Text),
            unit => Assert.Equal("Jour : Mardi | Présence requise : Oui", unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_HeadersFootersAndActiveContent_When_ExtractAsync_Then_ExtractsTextAndReturnsWarnings(
        string fileName)
    {
        // Given
        await using var package = CreatePackage(
            MinimalDocument("Contenu"),
            new Dictionary<string, string>
            {
                ["word/header1.xml"] = WordPart("En-tête"),
                ["word/footer1.xml"] = WordPart("Pied de page"),
                ["word/_rels/document.xml.rels"] = """
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="hyperlink" Target="https://example.com" TargetMode="External" />
                    </Relationships>
                    """,
                ["word/vbaProject.bin"] = "macro-not-executed",
                ["word/embeddings/oleObject1.bin"] = "object-not-opened"
            });
        var client = new MicrosoftWordContentExtractorClient();

        // When
        var result = await ExtractAsync(client, package, fileName, ".docm");

        // Then
        Assert.Equal(MicrosoftWordExtractionStatus.Success, result.Status);
        Assert.Equal(["En-tête", "Contenu", "Pied de page"], result.Units.Select(unit => unit.Text));
        Assert.Equal(
            [
                MicrosoftWordExtractionWarning.MacroIgnored,
                MicrosoftWordExtractionWarning.ExternalLinkIgnored,
                MicrosoftWordExtractionWarning.EmbeddedObjectIgnored
            ],
            result.Warnings);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyDocument_When_ExtractAsync_Then_ReturnsEmptyDocument(
        string fileName)
    {
        // Given
        await using var package = CreatePackage(MinimalDocument(string.Empty));
        var client = new MicrosoftWordContentExtractorClient();

        // When
        var result = await ExtractAsync(client, package, fileName);

        // Then
        Assert.Equal(MicrosoftWordExtractionStatus.EmptyDocument, result.Status);
        Assert.Empty(result.Units);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEncryptedOfficeSignature_When_ExtractAsync_Then_ReturnsEncryptedDocument(
        string fileName)
    {
        // Given
        var signature = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        await using var content = new MemoryStream(signature);
        var client = new MicrosoftWordContentExtractorClient();

        // When
        var result = await ExtractAsync(client, content, fileName);

        // Then
        Assert.Equal(MicrosoftWordExtractionStatus.EncryptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidPackage_When_ExtractAsync_Then_ReturnsCorruptedDocument(
        string fileName,
        byte[] invalidContent)
    {
        // Given
        await using var content = new MemoryStream([0x01, 0x02, 0x03, 0x04, .. invalidContent]);
        var client = new MicrosoftWordContentExtractorClient();

        // When
        var result = await ExtractAsync(client, content, fileName);

        // Then
        Assert.Equal(MicrosoftWordExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFileAboveTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(
        string fileName)
    {
        // Given
        await using var package = CreatePackage(MinimalDocument("Contenu"));
        var client = new MicrosoftWordContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.docx",
            null,
            package,
            MaximumFileSize + 1,
            MaximumFileSize,
            MaximumExpandedSize,
            MaximumCharacters,
            CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftWordExtractionStatus.TooLarge, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_CancellationRequested_When_ExtractAsync_Then_ThrowsOperationCanceledException(
        string fileName)
    {
        // Given
        await using var package = CreatePackage(MinimalDocument("Contenu"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var client = new MicrosoftWordContentExtractorClient();

        // When
        var exception = await Record.ExceptionAsync(() =>
            client.ExtractAsync(
                $"{fileName}.docx",
                null,
                package,
                package.Length,
                MaximumFileSize,
                MaximumExpandedSize,
                MaximumCharacters,
                cancellation.Token));

        // Then
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    private static Task<MicrosoftWordExtractionResult> ExtractAsync(
        MicrosoftWordContentExtractorClient client,
        Stream content,
        string fileName,
        string extension = ".docx") =>
        client.ExtractAsync(
            $"{fileName}{extension}",
            null,
            content,
            content.Length,
            MaximumFileSize,
            MaximumExpandedSize,
            MaximumCharacters,
            CancellationToken.None);

    private static MemoryStream CreatePackage(
        string documentXml,
        IReadOnlyDictionary<string, string>? additionalEntries = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "word/document.xml", documentXml);
            foreach (var entry in additionalEntries ?? new Dictionary<string, string>())
            {
                AddEntry(archive, entry.Key, entry.Value);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string MinimalDocument(string text) => $$"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:body><w:p><w:r><w:t>{{text}}</w:t></w:r></w:p></w:body>
        </w:document>
        """;

    private static string WordPart(string text) => $$"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:p><w:r><w:t>{{text}}</w:t></w:r></w:p>
        </w:hdr>
        """;
}
