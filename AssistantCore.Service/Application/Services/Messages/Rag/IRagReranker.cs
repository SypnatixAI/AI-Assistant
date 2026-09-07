using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public interface IRagReranker
{
    Task<IReadOnlyList<RagPassage>> RerankAsync(string query, IReadOnlyList<RagPassage> passages, CancellationToken cancellationToken = default);
}
