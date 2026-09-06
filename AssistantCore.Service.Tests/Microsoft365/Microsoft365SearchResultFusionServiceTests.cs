using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SearchResultFusionServiceTests
{
    [Theory, AutoDomainData]
    public void Given_DuplicateChunksAcrossQueries_When_Fuse_Then_DeduplicatesByReference(
        string duplicateChunkId,
        string uniqueChunkId)
    {
        // Given
        var service = new Microsoft365SearchResultFusionService();
        var firstResultSet = new[]
        {
            CreateRecord(duplicateChunkId, "Duplicate from original", 2.0d),
            CreateRecord(uniqueChunkId, "Unique", 3.0d)
        };
        var secondResultSet = new[]
        {
            CreateRecord(duplicateChunkId, "Duplicate from expansion", 4.0d)
        };

        // When
        var records = service.Fuse([firstResultSet, secondResultSet], 10);

        // Then
        Assert.Equal(2, records.Count);
        Assert.Single(records, record => record.Reference == duplicateChunkId);
        Assert.Single(records, record => record.Reference == uniqueChunkId);
    }

    [Theory, AutoDomainData]
    public void Given_ChunkAppearsInSeveralQueries_When_Fuse_Then_ReciprocalRankFusionBoostsIt(
        string duplicateChunkId,
        string uniqueChunkId)
    {
        // Given
        var service = new Microsoft365SearchResultFusionService();
        var firstResultSet = new[]
        {
            CreateRecord(uniqueChunkId, "Unique", 4.0d),
            CreateRecord(duplicateChunkId, "Duplicate", 1.0d)
        };
        var secondResultSet = new[]
        {
            CreateRecord(duplicateChunkId, "Duplicate", 2.0d)
        };

        // When
        var records = service.Fuse([firstResultSet, secondResultSet], 10).ToArray();

        // Then
        Assert.Equal(duplicateChunkId, records[0].Reference);
        Assert.True(records[0].RelevanceScore > records[1].RelevanceScore);
    }

    [Theory, AutoDomainData]
    public void Given_DuplicateChunkHasBetterRankInExpansion_When_Fuse_Then_KeepsBestRankedRecord(
        string duplicateChunkId,
        string otherChunkId)
    {
        // Given
        var service = new Microsoft365SearchResultFusionService();
        var firstResultSet = new[]
        {
            CreateRecord(otherChunkId, "Other", 4.0d),
            CreateRecord(duplicateChunkId, "Lower ranked duplicate", 1.0d)
        };
        var secondResultSet = new[]
        {
            CreateRecord(duplicateChunkId, "Best ranked duplicate", 2.0d)
        };

        // When
        var records = service.Fuse([firstResultSet, secondResultSet], 10);

        // Then
        var duplicate = Assert.Single(records, record => record.Reference == duplicateChunkId);
        Assert.Equal("Best ranked duplicate", duplicate.Title);
    }

    private static Microsoft365SearchRecord CreateRecord(
        string chunkId,
        string title,
        double score) =>
        new(
            "Microsoft365",
            title,
            "content",
            chunkId,
            "site-id",
            "drive-id",
            "drive-item-id",
            "https://contoso.example/document",
            DateTimeOffset.UtcNow,
            score);
}
