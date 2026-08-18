using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Messages.Evidence;

namespace AssistantCore.Service.Tests.Messages;

public sealed class EvidenceCitationResolverTests
{
    [Theory, AutoDomainData]
    public void Given_KnownDuplicateAndUnknownIds_When_Resolve_Then_ReturnsUniqueKnownEvidence(
        RetrievedEvidence firstEvidence,
        RetrievedEvidence secondEvidence,
        string unknownEvidenceId)
    {
        // Given
        var citedEvidenceIds = new[]
        {
            secondEvidence.EvidenceId,
            unknownEvidenceId,
            secondEvidence.EvidenceId,
            string.Empty,
            firstEvidence.EvidenceId
        };
        var resolver = new EvidenceCitationResolver();

        // When
        var results = resolver.Resolve(
            citedEvidenceIds,
            [firstEvidence, secondEvidence]);

        // Then
        Assert.Equal([secondEvidence, firstEvidence], results);
    }

    [Theory, AutoDomainData]
    public void Given_NoKnownId_When_Resolve_Then_ReturnsNoReplacement(
        RetrievedEvidence availableEvidence,
        string unknownEvidenceId)
    {
        // Given
        var resolver = new EvidenceCitationResolver();

        // When
        var results = resolver.Resolve(
            [unknownEvidenceId],
            [availableEvidence]);

        // Then
        Assert.Empty(results);
    }
}
