using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public interface IRetrievalQualityEvaluator
{
    Task<RetrievalQualityResult> EvaluateAsync(string query, IReadOnlyCollection<RagPassage> passages, CancellationToken cancellationToken = default);
}
