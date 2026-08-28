using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public sealed record ConversationMessageSourceItem(
    string SourceType,
    string Title,
    string? Url,
    string Reference,
    DateTimeOffset? SourceDate);

public sealed record ConversationMessageItem(
    Guid Id,
    MessageRole Role,
    string Content,
    MessageProcessingStatus ProcessingStatus,
    string? Model,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ConversationMessageSourceItem> Sources);

/// <summary>
/// Page de messages retournee dans l'ordre chronologique (du plus ancien au
/// plus recent), avec les coordonnees du message le plus ancien de la page
/// pour construire le prochain curseur sans ambiguite sur le sens de tri.
/// </summary>
public sealed record ConversationMessagePage(
    IReadOnlyList<ConversationMessageItem> Items,
    bool HasMore,
    DateTimeOffset? NextCursorCreatedAt,
    Guid? NextCursorId);
