using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Application.Services.Messages.Evidence;

public interface IEvidenceCitationResolver
{
    IReadOnlyCollection<RetrievedEvidence> Resolve(
        IReadOnlyCollection<string> citedEvidenceIds,
        IReadOnlyCollection<RetrievedEvidence> collectedEvidence);
}
