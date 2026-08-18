using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Evidence;

namespace AssistantCore.Service.Tests.Messages;

public sealed class EvidenceNormalizerTests
{
    [Theory, AutoDomainData]
    public void Given_ValidAndInvalidCandidates_When_Normalize_Then_FiltersTruncatesAndOrdersResults(
        string firstReference,
        string secondReference,
        DateTimeOffset occurredAt)
    {
        // Given
        var candidates = new[]
        {
            CreateCandidate(
                sourceType: null,
                reference: "missing-provenance",
                content: "Invalid",
                relevanceScore: 1),
            CreateCandidate(
                "CRM",
                firstReference,
                "123456789",
                relevanceScore: 0.9,
                occurredAt: occurredAt),
            CreateCandidate(
                "ERP",
                secondReference,
                "Longer content",
                relevanceScore: null,
                occurredAt: occurredAt)
        };
        var normalizer = new EvidenceNormalizer();

        // When
        var results = normalizer.Normalize(
            candidates,
            new EvidenceNormalizationOptions(
                MaximumContentLength: 5,
                MaximumResults: 2));

        // Then
        Assert.Equal(2, results.Count);
        Assert.Equal(firstReference, results.First().Reference);
        Assert.Equal("12345", results.First().Content);
        Assert.Equal(0.9, results.First().RelevanceScore);
        Assert.All(results, evidence => Assert.StartsWith("evidence-", evidence.EvidenceId));
    }

    [Theory, AutoDomainData]
    public void Given_DuplicateReferences_When_Normalize_Then_PrefersRelevanceThenCompleteness(
        string firstReference,
        string secondReference)
    {
        // Given
        var candidates = new[]
        {
            CreateCandidate("ERP", firstReference, "Longer but less relevant", 0.4),
            CreateCandidate("ERP", firstReference, "Relevant", 0.8),
            CreateCandidate("CRM", secondReference, "Short", 0.5),
            CreateCandidate("CRM", secondReference, "The most complete content", 0.5)
        };
        var normalizer = new EvidenceNormalizer();

        // When
        var results = normalizer.Normalize(
            candidates,
            new EvidenceNormalizationOptions(
                MaximumContentLength: 100,
                MaximumResults: 10));

        // Then
        Assert.Equal(2, results.Count);
        Assert.Equal(
            "Relevant",
            results.Single(evidence => evidence.Reference == firstReference).Content);
        Assert.Equal(
            "The most complete content",
            results.Single(evidence => evidence.Reference == secondReference).Content);
    }

    [Theory, AutoDomainData]
    public void Given_TheSameProvenance_When_Normalize_Then_CreatesAStableEvidenceId(
        string reference)
    {
        // Given
        var normalizer = new EvidenceNormalizer();
        var options = new EvidenceNormalizationOptions(
            MaximumContentLength: 100,
            MaximumResults: 10);
        var firstCandidate = CreateCandidate("ERP", reference, "First content", 0.5);
        var secondCandidate = CreateCandidate("ERP", reference, "Updated content", 0.9);

        // When
        var firstResult = Assert.Single(normalizer.Normalize([firstCandidate], options));
        var secondResult = Assert.Single(normalizer.Normalize([secondCandidate], options));

        // Then
        Assert.Equal(firstResult.EvidenceId, secondResult.EvidenceId);
    }

    [Theory, AutoDomainData]
    public void Given_ARelevanceScore_When_Serialize_Then_DoesNotExposeScore(
        RetrievedEvidence evidence,
        double relevanceScore)
    {
        // Given
        var scoredEvidence = evidence with { RelevanceScore = relevanceScore };

        // When
        var json = JsonSerializer.Serialize(scoredEvidence);

        // Then
        Assert.DoesNotContain("relevanceScore", json, StringComparison.OrdinalIgnoreCase);
    }

    private static EvidenceCandidate CreateCandidate(
        string? sourceType,
        string reference,
        string content,
        double? relevanceScore,
        DateTimeOffset? occurredAt = null) => new(
            sourceType,
            "Title",
            content,
            reference,
            Url: null,
            OccurredAt: occurredAt,
            RelevanceScore: relevanceScore);
}
