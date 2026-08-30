namespace AssistantCore.Service.Application.Configuration;

public sealed class ConversationOptions
{
    public const string SectionName = "Conversations";

    /// <summary>
    /// Longueur maximale acceptee pour un titre renomme. La colonne persistee accepte
    /// 200 caracteres : une valeur superieure est refusee au demarrage.
    /// </summary>
    public int MaximumTitleLength { get; init; }
}
