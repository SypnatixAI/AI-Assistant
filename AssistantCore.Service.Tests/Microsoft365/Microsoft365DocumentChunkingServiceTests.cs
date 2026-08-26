using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365DocumentChunkingServiceTests
{
    [Theory, AutoDomainData]
    public void Given_TheSameDocument_When_CreateChunks_Then_ProducesDeterministicPassages(
        Guid organizationId,
        Guid sourceId,
        string siteId,
        string driveId,
        string itemId,
        string version,
        string title,
        string url)
    {
        // Given
        var service = CreateService(maximumTokens: 12, overlapTokens: 2);
        var units = new[]
        {
            new Microsoft365ExtractedContentUnit(
                Microsoft365ExtractedContentUnitKind.Paragraph,
                0,
                string.Join(' ', Enumerable.Repeat("contenu", 30)),
                "word/document.xml")
        };

        // When
        var first = service.CreateChunks(
            organizationId, sourceId, siteId, driveId, itemId, version, title, url, null, units);
        var second = service.CreateChunks(
            organizationId, sourceId, siteId, driveId, itemId, version, title, url, null, units);

        // Then
        Assert.Equal(first, second);
        Assert.True(first.Count > 1);
        Assert.All(first, passage =>
        {
            Assert.Equal(itemId, passage.DriveItemId);
            Assert.True(passage.Content.Length <= 48);
        });
    }

    [Theory, AutoDomainData]
    public void Given_AnEmptyDocument_When_CreateChunks_Then_ReturnsNoPassages(
        Guid organizationId,
        Guid sourceId,
        string siteId,
        string driveId,
        string itemId,
        string version,
        string title)
    {
        // Given
        var service = CreateService(800, 100);

        // When
        var passages = service.CreateChunks(
            organizationId,
            sourceId,
            siteId,
            driveId,
            itemId,
            version,
            title,
            null,
            null,
            []);

        // Then
        Assert.Empty(passages);
    }

    private static Microsoft365DocumentChunkingService CreateService(
        int maximumTokens,
        int overlapTokens) =>
        new(Options.Create(new Microsoft365Options
        {
            ChunkMaximumTokens = maximumTokens,
            ChunkOverlapTokens = overlapTokens,
            MaximumChunksPerDocument = 100
        }));
}
