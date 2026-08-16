using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Evidence;

namespace AssistantCore.Service.Application.Services.Messages.Evidence;

public interface IEvidenceNormalizer
{
    IReadOnlyCollection<RetrievedEvidence> Normalize(
        IReadOnlyCollection<EvidenceCandidate> candidates,
        EvidenceNormalizationOptions options);
}
