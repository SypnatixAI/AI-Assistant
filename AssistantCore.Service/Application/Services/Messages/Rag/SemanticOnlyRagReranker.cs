using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public sealed class SemanticOnlyRagReranker : IRagReranker
{
    public Task<IReadOnlyList<RagPassage>> RerankAsync(string query, IReadOnlyList<RagPassage> passages, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(passages);
    }
}
