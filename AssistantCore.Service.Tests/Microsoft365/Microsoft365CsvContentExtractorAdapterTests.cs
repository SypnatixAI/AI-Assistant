using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365CsvContentExtractorAdapterTests
{
    [Theory, AutoDomainData]
    public void Given_ACsvExtension_When_CanExtract_Then_ReturnsTrue(string fileName)
    {
        // Given
        var adapter = CreateAdapter();

        // When
        var canExtract = adapter.CanExtract($"{fileName}.csv", null);

        // Then
        Assert.True(canExtract);
    }

    [Theory, AutoDomainData]
    public void Given_AnUnsupportedExtension_When_CanExtract_Then_ReturnsFalse(string fileName)
    {
        // Given
        var adapter = CreateAdapter();

        // When
        var canExtract = adapter.CanExtract($"{fileName}.json", null);

        // Then
        Assert.False(canExtract);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACsvFile_When_ExtractAsync_Then_MapsToSuccessWithATableUnit(string fileName)
    {
        // Given
        var adapter = CreateAdapter();
        var bytes = System.Text.Encoding.UTF8.GetBytes("Nom,Montant\nÉcran,450");
        var request = new Microsoft365ContentExtractionRequest(
            $"{fileName}.csv",
            "text/csv",
            new MemoryStream(bytes),
            bytes.Length);

        // When
        var result = await adapter.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ContentExtractionStatus.Success, result.Status);
        var unit = Assert.Single(result.Units);
        Assert.Equal(Microsoft365ExtractedContentUnitKind.Table, unit.Kind);
        Assert.Equal("Ligne 1 — Nom : Écran | Montant : 450", unit.Text);
    }

    private static Microsoft365CsvContentExtractorAdapter CreateAdapter() =>
        new(
            new MicrosoftCsvContentExtractorClient(),
            Options.Create(new Microsoft365Options()));
}
