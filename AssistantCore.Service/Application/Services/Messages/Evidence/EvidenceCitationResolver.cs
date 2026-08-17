using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Application.Services.Messages.Evidence;

public sealed class EvidenceCitationResolver : IEvidenceCitationResolver
{
    public IReadOnlyCollection<RetrievedEvidence> Resolve(
        IReadOnlyCollection<string> citedEvidenceIds,
        IReadOnlyCollection<RetrievedEvidence> collectedEvidence)
    {
        ArgumentNullException.ThrowIfNull(citedEvidenceIds);
        ArgumentNullException.ThrowIfNull(collectedEvidence);

        var evidenceById = collectedEvidence
            .Where(evidence => !string.IsNullOrWhiteSpace(evidence.EvidenceId))
            .GroupBy(evidence => evidence.EvidenceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var citedEvidence = new List<RetrievedEvidence>();
        var citedEvidenceIdsAlreadyAdded = new HashSet<string>(StringComparer.Ordinal);

        foreach (var evidenceId in citedEvidenceIds)
        {
            if (string.IsNullOrWhiteSpace(evidenceId)
                || !citedEvidenceIdsAlreadyAdded.Add(evidenceId)
                || !evidenceById.TryGetValue(evidenceId, out var evidence))
            {
                continue;
            }

            citedEvidence.Add(evidence);
        }

        return citedEvidence;
    }
}
