using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public interface IDedicatedRagReranker : IRagReranker
{
    decimal EstimateCost(IReadOnlyList<RagPassage> passages);
}
