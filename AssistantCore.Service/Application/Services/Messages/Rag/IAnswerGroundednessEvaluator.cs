using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public interface IAnswerGroundednessEvaluator
{
    Task<GroundednessResult> EvaluateAsync(string answer, IReadOnlyCollection<RagPassage> evidence, CancellationToken cancellationToken = default);
}
