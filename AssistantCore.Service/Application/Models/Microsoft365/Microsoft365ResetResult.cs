namespace AssistantCore.Service.Application.Models.Microsoft365;

/// <summary>
/// Resultat d'une reinitialisation de la selection Microsoft 365. Les compteurs
/// permettent de constater qu'un second appel ne supprime plus rien, donc que
/// l'operation est bien idempotente.
/// </summary>
public sealed record Microsoft365ResetResult(
    Guid OrganizationId,
    int DeletedSubscriptions,
    int DeletedSynchronizations,
    int DeletedWorkItems,
    int DeletedIndexedContents,
    int DeletedSources,
    int DeletedSearchChunks);
