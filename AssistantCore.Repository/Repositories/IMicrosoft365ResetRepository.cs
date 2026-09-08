namespace AssistantCore.Repository.Repositories;

/// <summary>
/// Remet une organisation dans l'etat ou son administrateur doit reselectionner
/// ses sites Microsoft 365, sans toucher a la connexion ni au consentement deja
/// accorde. Chaque operation est strictement limitee a l'organisation demandee.
/// </summary>
public interface IMicrosoft365ResetRepository
{
    /// <summary>
    /// Retourne les identifiants de passage indexes par l'organisation, afin que
    /// l'appelant puisse les supprimer d'Azure AI Search avant d'effacer les
    /// lignes correspondantes. Les identifiants sont lus avant toute suppression :
    /// une fois les lignes parties, plus rien ne permettrait de retrouver les
    /// documents restes dans l'index.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetIndexedChunkIdsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Efface la selection de sources et tout l'etat d'indexation de l'organisation :
    /// abonnements, synchronisations, travaux en attente, contenus et passages
    /// indexes, puis les sources elles-memes afin que la selection reparte de zero.
    /// La connexion Microsoft 365 et son consentement sont conserves.
    /// L'operation est idempotente : la rejouer sur une organisation deja
    /// reinitialisee ne change rien et ne leve pas d'erreur.
    /// </summary>
    Task<Microsoft365ResetCounts> ResetSelectionAndIndexingAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Nombre de lignes reellement supprimees par organisation, utile pour
/// journaliser un reset et pour verifier son idempotence.
/// </summary>
public sealed record Microsoft365ResetCounts(
    int Subscriptions,
    int Synchronizations,
    int DocumentWorks,
    int ListItemWorks,
    int IndexedContents,
    int Sources);
