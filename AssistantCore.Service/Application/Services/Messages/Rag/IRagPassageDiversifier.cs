using AssistantCore.Service.Application.Models.Messages.Rag;

namespace AssistantCore.Service.Application.Services.Messages.Rag;

public interface IRagPassageDiversifier
{
    /// <summary>
    /// Reordonne et elague une liste deja classee afin de limiter la redondance
    /// documentaire. Ne retourne jamais un passage absent de l'entree.
    /// </summary>
    IReadOnlyList<RagPassage> Diversify(IReadOnlyList<RagPassage> ranked);
}
