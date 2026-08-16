using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Messages.Evidence;

namespace AssistantCore.Service.Tests.Messages;

public sealed class EvidenceCitationValidatorTests
{
    [Theory, AutoDomainData]
    public void Given_KnownDuplicateAndUnknownIds_When_ResolveCitations_Then_ReturnsUniqueKnownEvidence(
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
        var validator = new EvidenceCitationValidator();

        // When
        var results = validator.ResolveCitations(
            citedEvidenceIds,
            [firstEvidence, secondEvidence]);

        // Then
        Assert.Equal([secondEvidence, firstEvidence], results);
    }

    [Theory, AutoDomainData]
    public void Given_NoKnownId_When_ResolveCitations_Then_ReturnsNoReplacement(
        RetrievedEvidence availableEvidence,
        string unknownEvidenceId)
    {
        // Given
        var validator = new EvidenceCitationValidator();

        // When
        var results = validator.ResolveCitations(
            [unknownEvidenceId],
            [availableEvidence]);

        // Then
        Assert.Empty(results);
    }
}
