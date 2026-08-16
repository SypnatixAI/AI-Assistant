using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Application.Services.Messages.Evidence;

public sealed class EvidenceCitationValidator : IEvidenceCitationValidator
{
    public IReadOnlyCollection<RetrievedEvidence> ResolveCitations(
        IReadOnlyCollection<string> citedEvidenceIds,
        IReadOnlyCollection<RetrievedEvidence> availableEvidence)
    {
        ArgumentNullException.ThrowIfNull(citedEvidenceIds);
        ArgumentNullException.ThrowIfNull(availableEvidence);

        var evidenceById = availableEvidence
            .Where(evidence => !string.IsNullOrWhiteSpace(evidence.EvidenceId))
            .GroupBy(evidence => evidence.EvidenceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        var resolvedEvidence = new List<RetrievedEvidence>();
        var resolvedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var citedEvidenceId in citedEvidenceIds)
        {
            if (string.IsNullOrWhiteSpace(citedEvidenceId)
                || !resolvedIds.Add(citedEvidenceId)
                || !evidenceById.TryGetValue(citedEvidenceId, out var evidence))
            {
                continue;
            }

            resolvedEvidence.Add(evidence);
        }

        return resolvedEvidence;
    }
}
