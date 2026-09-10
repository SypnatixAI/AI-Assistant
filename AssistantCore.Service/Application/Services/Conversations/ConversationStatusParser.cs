using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Conversations;

/// <summary>
/// Traduit le statut transmis par le frontend en valeur du domaine.
/// La suppression logique n'est jamais un statut : elle est portee par
/// <c>DeletedAt</c> et retire la conversation de toutes les lectures ordinaires,
/// quelle que soit la valeur demandee ici.
/// </summary>
public static class ConversationStatusParser
{
    /// <summary>
    /// Statut retenu lorsqu'une lecture n'en demande aucun : seules les
    /// conversations actives appartiennent a la liste des conversations recentes.
    /// </summary>
    public const ConversationStatus DefaultListingStatus = ConversationStatus.Active;

    public static ConversationStatus Parse(string status) =>
        status switch
        {
            nameof(ConversationStatus.Active) => ConversationStatus.Active,
            nameof(ConversationStatus.Archived) => ConversationStatus.Archived,
            _ => throw new BadRequestException("Status must be 'Active' or 'Archived'.")
        };

    /// <summary>
    /// Lit un statut optionnel. Une valeur absente ou vide vaut
    /// <see cref="DefaultListingStatus"/>; toute autre valeur inconnue est refusee.
    /// </summary>
    public static ConversationStatus ParseOrDefault(string? status) =>
        string.IsNullOrWhiteSpace(status) ? DefaultListingStatus : Parse(status);
}
