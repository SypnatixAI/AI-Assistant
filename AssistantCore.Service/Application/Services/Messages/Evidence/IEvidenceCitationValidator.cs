using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Application.Services.Messages.Evidence;

public interface IEvidenceCitationValidator
{
    IReadOnlyCollection<RetrievedEvidence> ResolveCitations(
        IReadOnlyCollection<string> citedEvidenceIds,
        IReadOnlyCollection<RetrievedEvidence> availableEvidence);
}
