using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftCsvContentExtractorClientTests
{
    private const long MaximumFileSize = 1_000_000;
    private const int MaximumRows = 10_000;
    private const int MaximumCharacters = 100_000;

    [Theory, AutoDomainData]
    public async Task Given_ACsvWithAHeaderAndOneRow_When_ExtractAsync_Then_ReturnsTheDocumentedLineFormat(
        string fileName)
    {
        // Given
        await using var content = ToStream("Nom,Montant\nÉcran,450");
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.csv", "text/csv", content, content.Length, MaximumFileSize, MaximumRows, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Equal("Ligne 1 — Nom : Écran | Montant : 450", Assert.Single(result.Units).Text);
    }

    [Theory, AutoDomainData]
    public async Task Given_MultipleDataRows_When_ExtractAsync_Then_NumbersEachDataRowStartingAtOne(string fileName)
    {
        // Given
        await using var content = ToStream("Nom,Montant\nÉcran,450\nClavier,80");
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.csv", "text/csv", content, content.Length, MaximumFileSize, MaximumRows, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(
            ["Ligne 1 — Nom : Écran | Montant : 450", "Ligne 2 — Nom : Clavier | Montant : 80"],
            result.Units.Select(unit => unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_AQuotedFieldContainingACommaAndAnEscapedQuote_When_ExtractAsync_Then_ParsesItCorrectly(
        string fileName)
    {
        // Given
        await using var content = ToStream("Nom,Description\nÉcran,\"24\"\"; noir, mat\"");
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.csv", "text/csv", content, content.Length, MaximumFileSize, MaximumRows, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal("Ligne 1 — Nom : Écran | Description : 24\"; noir, mat", Assert.Single(result.Units).Text);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnterminatedQuotedField_When_ExtractAsync_Then_ReturnsCorruptedDocument(string fileName)
    {
        // Given
        await using var content = ToStream("Nom,Description\nÉcran,\"non termine");
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.csv", "text/csv", content, content.Length, MaximumFileSize, MaximumRows, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_OnlyAHeaderRow_When_ExtractAsync_Then_ReturnsEmptyDocument(string fileName)
    {
        // Given
        await using var content = ToStream("Nom,Montant");
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.csv", "text/csv", content, content.Length, MaximumFileSize, MaximumRows, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.EmptyDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongExtension_When_ExtractAsync_Then_ReturnsUnsupportedFormat(string fileName)
    {
        // Given
        await using var content = ToStream("Nom,Montant\nÉcran,450");
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "text/csv", content, content.Length, MaximumFileSize, MaximumRows, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.UnsupportedFormat, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_MoreRowsThanTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        await using var content = ToStream("Nom,Montant\nÉcran,450\nClavier,80");
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.csv", "text/csv", content, content.Length, MaximumFileSize, maximumRows: 2, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyFile_When_ExtractAsync_Then_ReturnsEmptyDocument(string fileName)
    {
        // Given
        await using var content = ToStream(string.Empty);
        var client = new MicrosoftCsvContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.csv", "text/csv", content, content.Length, MaximumFileSize, MaximumRows, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.EmptyDocument, result.Status);
    }

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));
}
