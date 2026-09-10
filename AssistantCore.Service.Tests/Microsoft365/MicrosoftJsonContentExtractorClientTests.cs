using System.Text;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftJsonContentExtractorClientTests
{
    private const long MaximumFileSize = 1_000_000;
    private const int MaximumDepth = 32;
    private const int MaximumCharacters = 100_000;

    [Theory, AutoDomainData]
    public async Task Given_ANestedObject_When_ExtractAsync_Then_ReturnsDotPathsForEachLeaf(string fileName)
    {
        // Given
        await using var content = ToStream("""{"client":{"name":"Contoso","active":true},"count":3}""");
        var client = new MicrosoftJsonContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "application/json", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.Success, result.Status);
        Assert.Equal(
            ["client.name : Contoso", "client.active : true", "count : 3"],
            result.Units.Select(unit => unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_AnArray_When_ExtractAsync_Then_UsesIndexedPaths(string fileName)
    {
        // Given
        await using var content = ToStream("""{"items":[{"sku":"A1"},{"sku":"B2"}]}""");
        var client = new MicrosoftJsonContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "application/json", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(
            ["items[0].sku : A1", "items[1].sku : B2"],
            result.Units.Select(unit => unit.Text));
    }

    [Theory, AutoDomainData]
    public async Task Given_MalformedJson_When_ExtractAsync_Then_ReturnsCorruptedDocument(string fileName)
    {
        // Given
        await using var content = ToStream("""{"client": "Contoso" """);
        var client = new MicrosoftJsonContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "application/json", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.CorruptedDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_JsonDeeperThanTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        var deepJson = string.Concat(Enumerable.Repeat("{\"a\":", 40)) + "1" + string.Concat(Enumerable.Repeat("}", 40));
        await using var content = ToStream(deepJson);
        var client = new MicrosoftJsonContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "application/json", content, content.Length, MaximumFileSize, maximumDepth: 10, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyObject_When_ExtractAsync_Then_ReturnsEmptyDocument(string fileName)
    {
        // Given
        await using var content = ToStream("{}");
        var client = new MicrosoftJsonContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "application/json", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.EmptyDocument, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AWrongExtension_When_ExtractAsync_Then_ReturnsUnsupportedFormat(string fileName)
    {
        // Given
        await using var content = ToStream("""{"a":1}""");
        var client = new MicrosoftJsonContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.xml", "application/json", content, content.Length, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.UnsupportedFormat, result.Status);
    }

    [Theory, AutoDomainData]
    public async Task Given_AFileAboveTheConfiguredLimit_When_ExtractAsync_Then_ReturnsTooLarge(string fileName)
    {
        // Given
        await using var content = ToStream("""{"a":1}""");
        var client = new MicrosoftJsonContentExtractorClient();

        // When
        var result = await client.ExtractAsync(
            $"{fileName}.json", "application/json", content, MaximumFileSize + 1, MaximumFileSize, MaximumDepth, MaximumCharacters, CancellationToken.None);

        // Then
        Assert.Equal(MicrosoftTextBasedExtractionStatus.TooLarge, result.Status);
    }

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));
}
