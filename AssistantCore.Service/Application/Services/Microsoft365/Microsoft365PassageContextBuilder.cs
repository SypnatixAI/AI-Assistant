using System.Text;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

/// <summary>
/// Assemble le texte soumis a l'embedding : le contenu du passage precede de son
/// contexte hierarchique.
///
/// Un extrait comme « Maximum remboursable : 75 $ par jour » est retrouvable sans
/// que rien n'indique qu'il concerne les repas en deplacement international. Deux
/// sections d'un meme document peuvent ainsi contenir la meme phrase avec deux sens
/// differents; sans contexte, le moteur ne peut pas les distinguer.
///
/// Le resultat est purement deterministe et n'utilise que des metadonnees deja
/// extraites du document : aucun contexte n'est invente, et le meme passage produit
/// toujours le meme texte.
/// </summary>
public static class Microsoft365PassageContextBuilder
{
    public static string BuildEmbeddingContent(Microsoft365SearchPassage passage)
    {
        ArgumentNullException.ThrowIfNull(passage);

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(passage.Title))
        {
            builder.Append("[Document] ").AppendLine(passage.Title.Trim());
        }

        // La section n'est repetee que si le passage ne commence pas deja par elle,
        // ce qui arrive pour le premier extrait suivant un titre.
        if (!string.IsNullOrWhiteSpace(passage.SectionTitle)
            && !passage.Content.StartsWith(passage.SectionTitle.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            builder.Append("[Section] ").AppendLine(passage.SectionTitle.Trim());
        }

        if (builder.Length == 0)
        {
            // Un document sans titre ni section reste indexe tel quel : les formats
            // sans structure explicite continuent de fonctionner.
            return passage.Content;
        }

        return builder.AppendLine().Append(passage.Content).ToString();
    }
}
