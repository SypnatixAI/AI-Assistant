using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365JsonContentExtractorAdapterTests
{
    [Theory, AutoDomainData]
    public void Given_AJsonExtension_When_CanExtract_Then_ReturnsTrue(string fileName)
    {
        // Given
        var adapter = CreateAdapter();

        // When
        var canExtract = adapter.CanExtract($"{fileName}.json", null);

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
    public async Task Given_AJsonFile_When_ExtractAsync_Then_MapsToSuccessWithAParagraphUnit(string fileName)
    {
        // Given
        var adapter = CreateAdapter();
        var bytes = System.Text.Encoding.UTF8.GetBytes("""{"client":{"name":"Contoso"}}""");
        var request = new Microsoft365ContentExtractionRequest(
            $"{fileName}.json",
            "application/json",
            new MemoryStream(bytes),
            bytes.Length);

        // When
        var result = await adapter.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ContentExtractionStatus.Success, result.Status);
        var unit = Assert.Single(result.Units);
        Assert.Equal(Microsoft365ExtractedContentUnitKind.Paragraph, unit.Kind);
        Assert.Equal("client.name : Contoso", unit.Text);
    }

    private static Microsoft365JsonContentExtractorAdapter CreateAdapter() =>
        new(
            new MicrosoftJsonContentExtractorClient(),
            Options.Create(new Microsoft365Options()));
}
