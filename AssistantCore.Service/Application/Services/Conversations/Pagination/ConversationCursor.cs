namespace AssistantCore.Service.Application.Services.Conversations.Pagination;

/// <summary>
/// Position decodee dans la liste des conversations, selon l'ordre stable
/// impose par la documentation : UpdatedAt decroissant, puis Id decroissant
/// pour departager deux dates identiques.
/// </summary>
public sealed record ConversationCursor(DateTimeOffset UpdatedAt, Guid Id);
